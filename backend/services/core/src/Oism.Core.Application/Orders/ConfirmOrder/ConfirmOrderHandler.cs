using Oism.Core.Application.Inventory;
using Oism.SharedKernel;

namespace Oism.Core.Application.Orders.ConfirmOrder;

public sealed record ConfirmOrderCommand(Guid OrderId, Guid? ConfirmedBy, bool IncludeCost);

// UC-ORD-03: duyệt đơn là lúc hàng rời kho, trong một transaction. Trình tự: docs/design/flows/confirm-order.md.
public sealed class ConfirmOrderHandler(
    IUnitOfWork unitOfWork,
    IOrderRepository orders,
    IStockService stock,
    IEventPublisher events,
    IClock clock)
{
    public async Task<OrderDto> Handle(ConfirmOrderCommand command, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginAsync(ct);

        var order = await orders.GetForUpdateAsync(command.OrderId, ct)
            ?? throw new NotFoundException("đơn hàng");

        // Đơn không ở Reserved thì dừng ở đây, trước khi chạm tới số dư (AC-3).
        order.Confirm(clock.UtcNow);
        order.SnapshotCosts(await stock.ConsumeAsync(order, command.ConfirmedBy, ct));
        events.EnqueueOrderConfirmed(order);

        await transaction.CommitAsync(ct);
        return OrderDto.From(order, command.IncludeCost);
    }
}
