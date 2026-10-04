namespace Oism.Core.Domain.Orders;

public enum OrderStatus
{
    Draft,
    Reserved,
    Confirmed,
    Completed,
    Cancelled,
}

// Bảng bước chuyển của đơn: docs/design/state-machines.md mục "Đơn hàng". Bước không có ở đây bị từ chối (FR-ORD-02).
public static class OrderStateMachine
{
    private static readonly HashSet<(OrderStatus From, OrderStatus To)> Transitions =
    [
        (OrderStatus.Draft, OrderStatus.Reserved),
        (OrderStatus.Draft, OrderStatus.Cancelled),
        (OrderStatus.Reserved, OrderStatus.Confirmed),
        (OrderStatus.Reserved, OrderStatus.Cancelled),
        (OrderStatus.Confirmed, OrderStatus.Completed),
    ];

    public static bool CanMove(OrderStatus from, OrderStatus to) => Transitions.Contains((from, to));

    public static void EnsureCanMove(OrderStatus from, OrderStatus to)
    {
        if (!CanMove(from, to))
            throw new InvalidStateTransitionException("đơn hàng", from.ToString(), to.ToString());
    }
}
