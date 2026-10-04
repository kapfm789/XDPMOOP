namespace Oism.Contracts;

public sealed record SubmitOrderLine(string SkuCode, int Quantity, decimal UnitPrice, decimal Discount);

// docs/design/events.md mục "SubmitOrder". Command của `channel` gửi cho `core`; `core` tra SKU theo SkuCode.
public sealed record SubmitOrder(
    string Channel, string ExternalOrderId, Guid BranchId, DateTimeOffset PlacedAt, IReadOnlyList<SubmitOrderLine> Lines);
