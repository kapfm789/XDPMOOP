namespace Oism.Contracts;

public sealed record OrderRejectedDetail(string SkuCode, int Requested, int Available);

// docs/design/events.md mục "OrderRejected". Reason: InsufficientStock, UnknownSku, InactiveSku, InactiveBranch.
public sealed record OrderRejected(
    string Channel, string ExternalOrderId, Guid BranchId, string Reason, IReadOnlyList<OrderRejectedDetail> Details);
