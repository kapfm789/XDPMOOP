using Oism.Core.Domain.Orders;

namespace Oism.Core.Application.Orders;

public sealed record OrderItemDto(
    Guid Id, Guid SkuId, string SkuCode, string SkuName, int Quantity, decimal UnitPrice, decimal Discount)
{
    public static OrderItemDto From(OrderItem item) =>
        new(item.Id, item.SkuId, item.SkuCode, item.SkuName, item.Quantity, item.UnitPrice, item.Discount);
}

// Đơn kèm dòng đơn: docs/design/api/core.md mục "Đơn hàng".
public sealed record OrderDto(
    Guid Id,
    string OrderNumber,
    Guid BranchId,
    string Channel,
    string? ExternalOrderId,
    string Status,
    decimal TotalAmount,
    DateTimeOffset? ReservedUntil,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt,
    string? CancelReason,
    string? Note,
    Guid? CreatedBy,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OrderItemDto> Items)
{
    public static OrderDto From(Order order) => new(
        order.Id, order.OrderNumber, order.BranchId, order.Channel.ToString(), order.ExternalOrderId, order.Status.ToString(),
        order.TotalAmount, order.ReservedUntil, order.ConfirmedAt, order.CompletedAt, order.CancelledAt,
        order.CancelReason?.ToString(), order.Note, order.CreatedBy, order.CreatedAt,
        order.Items.Select(OrderItemDto.From).ToList());
}
