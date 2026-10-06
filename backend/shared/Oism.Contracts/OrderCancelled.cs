namespace Oism.Contracts;

// docs/design/events.md mục "OrderCancelled". Reason: Manual, Expired. ExternalOrderId null với đơn thủ công.
public sealed record OrderCancelled(
    Guid OrderId, string Channel, string? ExternalOrderId, Guid BranchId, string Reason, DateTimeOffset CancelledAt);
