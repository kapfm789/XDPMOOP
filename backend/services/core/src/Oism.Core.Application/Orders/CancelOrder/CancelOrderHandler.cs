using Oism.Contracts;
using Oism.Core.Application.Inventory;
using Oism.Core.Domain.Orders;
using Oism.SharedKernel;

namespace Oism.Core.Application.Orders.CancelOrder;

public sealed record CancelOrderCommand(Guid OrderId, OrderCancelReason Reason);

// UC-ORD-04 và UC-ORD-05: hủy tay và hết hạn dùng chung handler này, chỉ khác lý do.
// Trình tự: docs/design/flows/cancel-expire.md.
public sealed class CancelOrderHandler(
    IUnitOfWork unitOfWork,
    IOrderRepository orders,
    IStockService stock,
    IEventPublisher events,
    IClock clock)
{
    public async Task<OrderDto> Handle(CancelOrderCommand command, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginAsync(ct);

        var order = await orders.GetForUpdateAsync(command.OrderId, ct)
            ?? throw new NotFoundException("đơn hàng");

        // Hủy lại, hoặc job chạy lại: trả đơn hiện có, không tác động lần hai (T06).
        if (order.Status != OrderStatus.Cancelled)
        {
            // Đơn ở Confirmed hoặc Completed thì dừng ở đây, trước khi chạm tới số dư (T08).
            order.Cancel(command.Reason, clock.UtcNow);
            await stock.ReleaseAsync(order, ct);
            events.Enqueue(new OrderCancelled(
                order.Id, order.Channel.ToString(), order.ExternalOrderId, order.BranchId, command.Reason.ToString(),
                order.CancelledAt!.Value));
        }

        await transaction.CommitAsync(ct);
        return OrderDto.From(order);
    }
}
