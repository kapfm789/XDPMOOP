using System.Text.Json;
using System.Text.Json.Serialization;
using Oism.Channel.Domain;

namespace Oism.Channel.Application.Webhooks.Adapters;

// { "message_id", "seller_id", "order": { "order_number", "created_at" (ISO 8601),
//   "items": [{ "sku", "qty", "paid_price", "voucher" }] } }
public sealed class LazadaAdapter : ChannelAdapter
{
    public override SalesChannel Channel => SalesChannel.Lazada;

    protected override string EventIdField => "message_id";

    protected override string ShopIdField => "seller_id";

    public override string Build(WebhookEnvelope envelope, CanonicalOrder order) => JsonSerializer.Serialize(new Payload(
        envelope.EventId, envelope.ShopId,
        new Order(
            order.ExternalOrderId, order.PlacedAt,
            order.Lines.Select(line => new Item(line.SkuCode, line.Quantity, line.UnitPrice, line.Discount)).ToList())));

    protected override CanonicalOrder Convert(JsonElement payload)
    {
        var order = payload.Deserialize<Payload>()!.Order;
        return CanonicalOrder.From(
            order?.OrderNumber, order?.CreatedAt,
            order?.Items?.Select(item => (item.Sku, item.Qty, item.PaidPrice, item.Voucher)));
    }

    private sealed record Payload(
        [property: JsonPropertyName("message_id")] string? MessageId,
        [property: JsonPropertyName("seller_id")] string? SellerId,
        [property: JsonPropertyName("order")] Order? Order);

    private sealed record Order(
        [property: JsonPropertyName("order_number")] string? OrderNumber,
        [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt,
        [property: JsonPropertyName("items")] IReadOnlyList<Item>? Items);

    private sealed record Item(
        [property: JsonPropertyName("sku")] string? Sku,
        [property: JsonPropertyName("qty")] int? Qty,
        [property: JsonPropertyName("paid_price")] decimal? PaidPrice,
        [property: JsonPropertyName("voucher")] decimal? Voucher);
}
