namespace Oism.Contracts;

public sealed record OrderReservedLine(Guid SkuId, int Quantity);

// docs/design/events.md mục "OrderReserved". ExternalOrderId null với đơn thủ công.
public sealed record OrderReserved(
    Guid OrderId,
    string OrderNumber,
    string Channel,
    string? ExternalOrderId,
    Guid BranchId,
    decimal TotalAmount,
    DateTimeOffset ReservedUntil,
    IReadOnlyList<OrderReservedLine> Lines);
