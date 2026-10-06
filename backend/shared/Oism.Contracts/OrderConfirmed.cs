namespace Oism.Contracts;

public sealed record OrderConfirmedLine(
    Guid OrderItemId, Guid SkuId, int Quantity, decimal UnitPrice, decimal Discount, decimal CostPrice);

// docs/design/events.md mục "OrderConfirmed". Phát cho mọi kênh, kể cả POS và Admin.
// CostPrice là giá vốn đã chốt lúc xác nhận; bên nhận không tự tính lại.
public sealed record OrderConfirmed(
    Guid OrderId, string Channel, Guid BranchId, DateTimeOffset ConfirmedAt, IReadOnlyList<OrderConfirmedLine> Lines);
