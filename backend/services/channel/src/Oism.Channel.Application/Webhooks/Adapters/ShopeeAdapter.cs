using System.Text.Json;
using System.Text.Json.Serialization;
using Oism.Channel.Domain;

namespace Oism.Channel.Application.Webhooks.Adapters;

// { "event_id", "shop_id", "ordersn", "create_time" (giây epoch), "item_list": [{ "model_sku", "quantity", "price", "discount" }] }
public sealed class ShopeeAdapter : ChannelAdapter
{
    public override SalesChannel Channel => SalesChannel.Shopee;

    protected override string EventIdField => "event_id";

    protected override string ShopIdField => "shop_id";

    public override string Build(WebhookEnvelope envelope, CanonicalOrder order) => JsonSerializer.Serialize(new Payload(
        envelope.EventId, envelope.ShopId, order.ExternalOrderId, order.PlacedAt.ToUnixTimeSeconds(),
        order.Lines.Select(line => new Item(line.SkuCode, line.Quantity, line.UnitPrice, line.Discount)).ToList()));

    protected override CanonicalOrder Convert(JsonElement payload)
    {
        var order = payload.Deserialize<Payload>()!;
        return CanonicalOrder.From(
            order.OrderSn, FromEpochSeconds(order.CreateTime),
            order.ItemList?.Select(item => (item.ModelSku, item.Quantity, item.Price, item.Discount)));
    }

    private sealed record Payload(
        [property: JsonPropertyName("event_id")] string? EventId,
        [property: JsonPropertyName("shop_id")] string? ShopId,
        [property: JsonPropertyName("ordersn")] string? OrderSn,
        [property: JsonPropertyName("create_time")] long? CreateTime,
        [property: JsonPropertyName("item_list")] IReadOnlyList<Item>? ItemList);

    private sealed record Item(
        [property: JsonPropertyName("model_sku")] string? ModelSku,
        [property: JsonPropertyName("quantity")] int? Quantity,
        [property: JsonPropertyName("price")] decimal? Price,
        [property: JsonPropertyName("discount")] decimal? Discount);
}
