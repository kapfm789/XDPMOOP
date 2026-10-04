using FluentValidation;
using FluentValidation.Results;
using Oism.Contracts;
using Oism.Core.Application.Inventory;
using Oism.Core.Application.References;
using Oism.Core.Domain.Inventory;
using Oism.Core.Domain.Orders;
using Oism.Core.Domain.References;
using Oism.SharedKernel;

namespace Oism.Core.Application.Orders.ReserveStock;

// UC-ORD-01 và UC-ORD-02: tạo đơn theo Canonical Order và giữ hàng trong một transaction.
// Trình tự: docs/design/flows/reserve-stock.md. Hai đường vào dùng chung handler này:
// POST /orders (ReserveStockCommand) và consumer SubmitOrder.
public sealed class ReserveStockHandler(
    IUnitOfWork unitOfWork,
    IReferenceRepository references,
    IOrderRepository orders,
    IStockService stock,
    IEventPublisher events,
    IClock clock,
    ReservationSettings settings)
{
    private static readonly ReserveStockValidator Validator = new();

    public async Task<OrderDto> Handle(ReserveStockCommand command, CancellationToken ct)
    {
        Validator.ValidateAndThrow(command);

        await using var transaction = await unitOfWork.BeginAsync(ct);

        // Chi nhánh và SKU tra trong tenant hiện tại (docs/architecture/multi-tenancy.md quy tắc 4).
        var branch = await references.FindBranchAsync(command.BranchId, ct)
            ?? throw new ReferenceNotReadyException("Chi nhánh", command.BranchId);
        branch.EnsureActive();

        var skus = (await references.ListSkusAsync(command.Items.Select(item => item.SkuId).ToHashSet(), ct))
            .ToDictionary(sku => sku.SkuId);

        var now = clock.UtcNow;
        var order = Order.Create(
            command.BranchId, command.Channel, command.ExternalOrderId, idempotencyKey: null, command.Note, command.CreatedBy, now);
        for (var index = 0; index < command.Items.Count; index++)
        {
            var line = command.Items[index];
            if (!skus.TryGetValue(line.SkuId, out var sku))
                throw new ReferenceNotReadyException("SKU", line.SkuId);
            sku.EnsureActive();

            // UC-ORD-02 AC-3: không nhập đơn giá thì lấy giá lẻ hiện tại.
            var unitPrice = line.UnitPrice ?? sku.RetailPrice;
            var discount = line.Discount ?? 0;
            if (!OrderItem.IsValidDiscount(line.Quantity, unitPrice, discount))
            {
                throw new ValidationException(
                    [new ValidationFailure($"Items[{index}].Discount", "Giảm giá không được vượt thành tiền của dòng")]);
            }

            order.AddItem(sku.SkuId, sku.SkuCode, sku.Name, line.Quantity, unitPrice, discount);
        }

        // Chèn đơn ở Draft trước khi khóa số dư: chứng từ đứng trước số dư trong thứ tự khóa, và chỉ mục unique
        // (tenant_id, channel, external_order_id) chặn đơn của sàn tới lần hai trước khi nó kịp giữ thêm hàng.
        orders.Add(order);
        await transaction.SaveAsync(ct);

        var reservedUntil = now.AddMinutes(settings.HoldMinutes);
        await stock.ReserveAsync(order, reservedUntil, ct);
        order.MarkReserved(reservedUntil);
        events.Enqueue(new OrderReserved(
            order.Id, order.OrderNumber, order.Channel.ToString(), order.ExternalOrderId, order.BranchId, order.TotalAmount,
            reservedUntil, order.Items.Select(item => new OrderReservedLine(item.SkuId, item.Quantity)).ToList()));

        await transaction.CommitAsync(ct);
        return OrderDto.From(order);
    }

    // UC-ORD-01: đơn online từ command SubmitOrder của `channel`. Giữ được hàng thì phát OrderReserved như đơn thủ công;
    // không giữ được thì transaction giữ hàng rollback, rồi một transaction thứ hai chỉ ghi outbox OrderRejected.
    public async Task Handle(SubmitOrder message, CancellationToken ct)
    {
        if (await TryReserveAsync(message, ct) is not { } rejection)
            return;

        await using var transaction = await unitOfWork.BeginAsync(ct);
        events.Enqueue(rejection);
        await transaction.CommitAsync(ct);
    }

    // Null khi đơn đã được giữ hàng, hoặc khi đơn của sàn đã có từ trước (UC-ORD-01 AC-4).
    private async Task<OrderRejected?> TryReserveAsync(SubmitOrder message, CancellationToken ct)
    {
        var requested = message.Lines
            .GroupBy(line => line.SkuCode)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity));
        var skus = (await references.ListSkusByCodeAsync(requested.Keys, ct)).ToDictionary(sku => sku.SkuCode);

        var unknown = requested.Keys.Where(code => !skus.ContainsKey(code)).ToList();
        if (unknown.Count > 0)
            return Rejected("UnknownSku", unknown.Select(code => new OrderRejectedDetail(code, requested[code], 0)));

        var command = new ReserveStockCommand(
            message.BranchId,
            Enum.Parse<OrderChannel>(message.Channel),
            Note: null,
            message.Lines.Select(line => new ReserveStockLine(skus[line.SkuCode].SkuId, line.Quantity, line.UnitPrice, line.Discount)).ToList(),
            CreatedBy: null,
            message.ExternalOrderId);
        var codes = skus.Values.ToDictionary(sku => sku.SkuId, sku => sku.SkuCode);

        try
        {
            await Handle(command, ct);
            return null;
        }
        catch (DuplicateException)
        {
            // Cùng mã đơn của sàn tới lần hai: đơn đã có, không giữ thêm hàng và không phát gì.
            return null;
        }
        catch (InsufficientStockException shortage)
        {
            return Rejected("InsufficientStock", shortage.Shortages.Select(line =>
                new OrderRejectedDetail(codes[line.SkuId], line.Requested, line.Available)));
        }
        catch (InactiveReferenceException inactive)
        {
            return Rejected(inactive.Reason, codes.TryGetValue(inactive.Id, out var code)
                ? [new OrderRejectedDetail(code, requested[code], 0)]
                : []);
        }
        catch (ReferenceNotReadyException)
        {
            // SKU đã được tra theo mã ở trên, nên chỉ còn chi nhánh chưa có bản sao ở `core`.
            return Rejected("InactiveBranch", []);
        }

        OrderRejected Rejected(string reason, IEnumerable<OrderRejectedDetail> details) =>
            new(message.Channel, message.ExternalOrderId, message.BranchId, reason, details.ToList());
    }
}
