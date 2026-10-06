using Oism.SharedKernel;

namespace Oism.Core.Domain.Orders;

public enum ReservationStatus
{
    Active,
    Consumed,
    Released,
}

// Phần giữ hàng của một dòng đơn: docs/design/data-model/core.md mục "reservations".
// Tổng số lượng các phần giữ Active của một SKU tại một chi nhánh luôn bằng `reserved` trên dòng số dư;
// hai giá trị chỉ đổi cùng nhau qua IStockService (docs/design/state-machines.md mục "Phần giữ hàng").
public sealed class Reservation : ITenantOwned
{
    private Reservation()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid OrderItemId { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid SkuId { get; private set; }

    public int Quantity { get; private set; }

    public ReservationStatus Status { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    // Null khi còn Active.
    public DateTimeOffset? ClosedAt { get; private set; }

    public static Reservation Hold(Guid branchId, OrderItem item, DateTimeOffset expiresAt, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        OrderId = item.OrderId,
        OrderItemId = item.Id,
        BranchId = branchId,
        SkuId = item.SkuId,
        Quantity = item.Quantity,
        Status = ReservationStatus.Active,
        ExpiresAt = expiresAt,
        CreatedAt = now,
    };

    // Đơn được xác nhận. Chỉ IStockService gọi, cùng lúc với việc giảm `reserved` trên dòng số dư.
    public void Consume(DateTimeOffset now) => Close(ReservationStatus.Consumed, now);

    // Đơn bị hủy hoặc hết hạn. Chỉ IStockService gọi, cùng lúc với việc giảm `reserved` trên dòng số dư.
    public void Release(DateTimeOffset now) => Close(ReservationStatus.Released, now);

    private void Close(ReservationStatus status, DateTimeOffset now)
    {
        if (Status != ReservationStatus.Active)
            throw new InvalidStateTransitionException("phần giữ hàng", Status.ToString(), status.ToString());

        Status = status;
        ClosedAt = now;
    }
}
