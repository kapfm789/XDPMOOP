using System.Text.Json.Serialization;
using Oism.Contracts;
using Oism.Core.Domain.Orders;

namespace Oism.Core.Application.Orders;

// CostPrice chỉ có với Owner và chỉ sau khi đơn được xác nhận; với vai trò khác trường này không nằm trong phản hồi
// (docs/design/api/core.md mục "Đơn hàng").
public sealed record OrderItemDto(
    Guid Id,
    Guid SkuId,
    string SkuCode,
    string SkuName,
    int Quantity,
    decimal UnitPrice,
    decimal Discount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? CostPrice = null)
{
    public static OrderItemDto From(OrderItem item, bool includeCost) => new(
        item.Id, item.SkuId, item.SkuCode, item.SkuName, item.Quantity, item.UnitPrice, item.Discount,
        includeCost ? item.CostPrice : null);
}

public sealed record PaymentDto(string Method, decimal Amount, Guid ConfirmedBy, DateTimeOffset ConfirmedAt)
{
    public static PaymentDto From(Payment payment) =>
        new(payment.Method.ToString(), payment.Amount, payment.ConfirmedBy, payment.ConfirmedAt);
}

public sealed record ReservationDto(
    Guid Id, Guid OrderItemId, Guid SkuId, int Quantity, string Status, DateTimeOffset ExpiresAt, DateTimeOffset? ClosedAt)
{
    public static ReservationDto From(Reservation hold) =>
        new(hold.Id, hold.OrderItemId, hold.SkuId, hold.Quantity, hold.Status.ToString(), hold.ExpiresAt, hold.ClosedAt);
}

// Đơn kèm dòng đơn: docs/design/api/core.md mục "Đơn hàng". Payment chỉ có với đơn POS;
// Reservations chỉ có trong phản hồi của GET /orders/{id}.
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
    IReadOnlyList<OrderItemDto> Items,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PaymentDto? Payment = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ReservationDto>? Reservations = null)
{
    public static OrderDto From(Order order, bool includeCost = false, IEnumerable<Reservation>? reservations = null) => new(
        order.Id, order.OrderNumber, order.BranchId, order.Channel.ToString(), order.ExternalOrderId, order.Status.ToString(),
        order.TotalAmount, order.ReservedUntil, order.ConfirmedAt, order.CompletedAt, order.CancelledAt,
        order.CancelReason?.ToString(), order.Note, order.CreatedBy, order.CreatedAt,
        order.Items.Select(item => OrderItemDto.From(item, includeCost)).ToList(),
        order.Payment is null ? null : PaymentDto.From(order.Payment),
        reservations?.Select(ReservationDto.From).ToList());
}

// Truy vấn chỉ đọc của Orders, cài đặt ở Infrastructure.
public interface IOrderQueries
{
    // Đơn kèm dòng đơn và thanh toán, mới nhất trước.
    Task<PagedResult<OrderDto>> ListAsync(ListOrders.ListOrdersQuery query, CancellationToken ct);

    // Đơn kèm dòng đơn, phần giữ hàng và thanh toán. Null khi đơn không có trong tenant.
    Task<OrderDto?> FindAsync(Guid id, bool includeCost, CancellationToken ct);
}

public static class OrderEvents
{
    // OrderConfirmed mang giá bán và giá vốn đã chốt của từng dòng (docs/design/events.md). Gọi sau SnapshotCosts.
    public static void EnqueueOrderConfirmed(this IEventPublisher events, Order order) =>
        events.Enqueue(new OrderConfirmed(
            order.Id, order.Channel.ToString(), order.BranchId, order.ConfirmedAt!.Value,
            order.Items
                .Select(item => new OrderConfirmedLine(
                    item.Id, item.SkuId, item.Quantity, item.UnitPrice, item.Discount, item.CostPrice!.Value))
                .ToList()));
}
