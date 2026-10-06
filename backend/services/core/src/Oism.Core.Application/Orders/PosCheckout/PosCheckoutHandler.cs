using FluentValidation;
using FluentValidation.Results;
using Oism.Core.Application.Inventory;
using Oism.Core.Application.References;
using Oism.Core.Domain.Orders;
using Oism.SharedKernel;

namespace Oism.Core.Application.Orders.PosCheckout;

// UC-POS-02: tạo đơn, giữ hàng, xác nhận, ghi thanh toán và hoàn tất trong một transaction (FR-POS-04).
// Trình tự: docs/design/flows/pos-checkout.md.
public sealed class PosCheckoutHandler(
    IUnitOfWork unitOfWork,
    IReferenceRepository references,
    IOrderRepository orders,
    IStockService stock,
    IEventPublisher events,
    IClock clock)
{
    private static readonly PosCheckoutValidator Validator = new();

    public async Task<PosCheckoutResult> Handle(PosCheckoutCommand command, CancellationToken ct)
    {
        Validator.ValidateAndThrow(command);
        var key = Guid.Parse(command.IdempotencyKey!).ToString();

        // Bấm hai lần hoặc mạng gửi lại: trả đúng đơn đã tạo, không tác động lần hai (T03).
        if (await orders.FindByIdempotencyKeyAsync(key, ct) is { } existing)
            return new PosCheckoutResult(OrderDto.From(existing, command.IncludeCost), Created: false);

        try
        {
            return new PosCheckoutResult(await CheckoutAsync(command, key, ct), Created: true);
        }
        catch (DuplicateException)
        {
            // Hai request đồng thời cùng khóa: request thua bị chặn ở chỉ mục unique (tenant_id, idempotency_key),
            // transaction của nó đã rollback; đọc lại đơn của request thắng.
            if (await orders.FindByIdempotencyKeyAsync(key, ct) is not { } winner)
                throw;
            return new PosCheckoutResult(OrderDto.From(winner, command.IncludeCost), Created: false);
        }
    }

    private async Task<OrderDto> CheckoutAsync(PosCheckoutCommand command, string key, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginAsync(ct);

        var now = clock.UtcNow;
        var order = await DraftOrder.CreateAsync(
            references, command.BranchId, OrderChannel.POS, externalOrderId: null, key, note: null,
            command.Items, command.CashierId, now, ct);
        var payment = command.Payment!;
        if (payment.Amount < order.TotalAmount)
            throw new ValidationException([new ValidationFailure("Payment.Amount", "Số tiền thanh toán nhỏ hơn tổng đơn")]);

        // Chèn đơn ở Draft trước khi khóa số dư: chứng từ đứng trước số dư trong thứ tự khóa,
        // và chỉ mục unique của idempotency_key lên tiếng trước khi đơn kịp giữ hàng.
        orders.Add(order);
        await transaction.SaveAsync(ct);

        // Phần giữ chỉ sống bên trong transaction này nên hết hạn ngay tại `now`.
        await stock.ReserveAsync(order, now, ct);
        order.MarkReserved(now);
        // Phần giữ vừa tạo phải xuống database để bước Consume đọc lại được.
        await transaction.SaveAsync(ct);

        order.Confirm(now);
        order.SnapshotCosts(await stock.ConsumeAsync(order, command.CashierId, ct));
        order.Pay(Enum.Parse<PaymentMethod>(payment.Method!), payment.Amount, command.CashierId, now);
        order.Complete(now);
        // Đơn POS không phát OrderReserved (docs/design/state-machines.md).
        events.EnqueueOrderConfirmed(order);

        await transaction.CommitAsync(ct);
        return OrderDto.From(order, command.IncludeCost);
    }
}
