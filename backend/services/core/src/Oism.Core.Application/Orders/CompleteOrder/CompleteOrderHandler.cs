using Oism.SharedKernel;

namespace Oism.Core.Application.Orders.CompleteOrder;

public sealed record CompleteOrderCommand(Guid OrderId, bool IncludeCost);

// UC-ORD-06: đánh dấu đơn đã giao xong. Không tác động tồn và không phát event (docs/design/state-machines.md).
public sealed class CompleteOrderHandler(IUnitOfWork unitOfWork, IOrderRepository orders, IClock clock)
{
    public async Task<OrderDto> Handle(CompleteOrderCommand command, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginAsync(ct);

        var order = await orders.GetForUpdateAsync(command.OrderId, ct)
            ?? throw new NotFoundException("đơn hàng");
        order.Complete(clock.UtcNow);

        await transaction.CommitAsync(ct);
        return OrderDto.From(order, command.IncludeCost);
    }
}
