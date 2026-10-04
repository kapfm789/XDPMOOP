using FluentValidation;
using FluentValidation.Results;
using Oism.Core.Application.References;
using Oism.Core.Domain.Orders;
using Oism.Core.Domain.References;

namespace Oism.Core.Application.Orders.ReserveStock;

// UC-ORD-02: tạo đơn theo Canonical Order. Trình tự: docs/design/flows/reserve-stock.md.
// ponytail: W2-04 dừng ở bước chèn đơn Draft. W2-05 thêm bước giữ hàng (IStockService.Reserve, đơn sang Reserved)
// và W2-06 thêm outbox, cả hai nằm ngay trước CommitAsync, trong cùng transaction này.
public sealed class ReserveStockHandler(
    IUnitOfWork unitOfWork, IReferenceRepository references, IOrderRepository orders, IClock clock)
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

        var order = Order.Create(
            command.BranchId, command.Channel, externalOrderId: null, idempotencyKey: null, command.Note, command.CreatedBy,
            clock.UtcNow);
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

        orders.Add(order);

        await transaction.CommitAsync(ct);
        return OrderDto.From(order);
    }
}
