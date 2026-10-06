using Oism.SharedKernel;

namespace Oism.Core.Domain.Orders;

public enum OrderChannel
{
    POS,
    Admin,
    Shopee,
    TikTok,
    Lazada,
}

public enum OrderCancelReason
{
    Manual,
    Expired,
}

// Canonical Order: đơn từ POS, admin và các sàn dùng chung một cấu trúc (FR-ORD-01).
// Bảng: docs/design/data-model/core.md mục "orders". Trạng thái chỉ đổi qua các phương thức dưới đây.
public sealed class Order : ITenantOwned
{
    private readonly List<OrderItem> _items = [];

    private Order()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid BranchId { get; private set; }

    public string OrderNumber { get; private set; } = null!;

    public OrderChannel Channel { get; private set; }

    // Mã đơn của sàn; null với đơn POS và đơn thủ công.
    public string? ExternalOrderId { get; private set; }

    // Chỉ đơn POS.
    public string? IdempotencyKey { get; private set; }

    public OrderStatus Status { get; private set; }

    public decimal TotalAmount { get; private set; }

    public DateTimeOffset? ReservedUntil { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public OrderCancelReason? CancelReason { get; private set; }

    public string? Note { get; private set; }

    // Null với đơn từ sàn.
    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items;

    // Chỉ đơn POS.
    public Payment? Payment { get; private set; }

    public static Order Create(
        Guid branchId,
        OrderChannel channel,
        string? externalOrderId,
        string? idempotencyKey,
        string? note,
        Guid? createdBy,
        DateTimeOffset now)
    {
        var id = Guid.NewGuid();
        return new Order
        {
            Id = id,
            BranchId = branchId,
            // ponytail: mã hiển thị lấy từ id của đơn, không theo số thứ tự; cần mã liền mạch thì thêm bộ đếm theo tenant.
            OrderNumber = $"DH{id.ToString("N")[..12].ToUpperInvariant()}",
            Channel = channel,
            ExternalOrderId = externalOrderId,
            IdempotencyKey = idempotencyKey,
            Status = OrderStatus.Draft,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedBy = createdBy,
            CreatedAt = now,
        };
    }

    // Mã, tên và đơn giá của SKU được chụp lại lúc tạo đơn; đổi giá sau đó không đổi dòng đơn (UC-PROD-04 AC-4).
    public OrderItem AddItem(Guid skuId, string skuCode, string skuName, int quantity, decimal unitPrice, decimal discount)
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException("Items can only be added to a draft order.");

        var item = OrderItem.Create(Id, skuId, skuCode, skuName, quantity, unitPrice, discount);
        _items.Add(item);
        TotalAmount += item.LineTotal;
        return item;
    }

    public void MarkReserved(DateTimeOffset reservedUntil)
    {
        MoveTo(OrderStatus.Reserved);
        ReservedUntil = reservedUntil;
    }

    public void Confirm(DateTimeOffset now)
    {
        MoveTo(OrderStatus.Confirmed);
        ConfirmedAt = now;
    }

    // Giá vốn bình quân lúc xuất, tra theo Id của dòng đơn; ghi một lần ngay sau Confirm (FR-COST-02).
    public void SnapshotCosts(IReadOnlyDictionary<Guid, decimal> costPrices)
    {
        foreach (var item in _items)
            item.FixCostPrice(costPrices[item.Id]);
    }

    // Đơn POS: thu ngân đã nhận đủ tiền trước khi bấm thanh toán (ADR-0007).
    public void Pay(PaymentMethod method, decimal amount, Guid confirmedBy, DateTimeOffset now)
    {
        if (Payment is not null)
            throw new InvalidOperationException("The order is already paid.");
        ArgumentOutOfRangeException.ThrowIfLessThan(amount, TotalAmount);

        Payment = Payment.Create(Id, method, amount, confirmedBy, now);
    }

    public void Complete(DateTimeOffset now)
    {
        MoveTo(OrderStatus.Completed);
        CompletedAt = now;
    }

    public void Cancel(OrderCancelReason reason, DateTimeOffset now)
    {
        MoveTo(OrderStatus.Cancelled);
        CancelReason = reason;
        CancelledAt = now;
    }

    private void MoveTo(OrderStatus next)
    {
        OrderStateMachine.EnsureCanMove(Status, next);
        Status = next;
    }
}
