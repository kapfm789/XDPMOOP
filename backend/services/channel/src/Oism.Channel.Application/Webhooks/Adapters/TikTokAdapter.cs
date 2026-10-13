using System.Text.Json;
using System.Text.Json.Serialization;
using Oism.Channel.Domain;

namespace Oism.Channel.Application.Webhooks.Adapters;

// { "id", "shop_id", "data": { "order_id", "create_time" (giây epoch),
//   "line_items": [{ "seller_sku", "quantity", "sale_price", "seller_discount" }] } }
public sealed class TikTokAdapter : ChannelAdapter
{
    public override SalesChannel Channel => SalesChannel.TikTok;

    protected override string EventIdField => "id";

    protected override string ShopIdField => "shop_id";

    public override string Build(WebhookEnvelope envelope, CanonicalOrder order) => JsonSerializer.Serialize(new Payload(
        envelope.EventId, envelope.ShopId,
        new Data(
            order.ExternalOrderId, order.PlacedAt.ToUnixTimeSeconds(),
            order.Lines.Select(line => new LineItem(line.SkuCode, line.Quantity, line.UnitPrice, line.Discount)).ToList())));

    protected override CanonicalOrder Convert(JsonElement payload)
    {
        var data = payload.Deserialize<Payload>()!.Data;
        return CanonicalOrder.From(
            data?.OrderId, FromEpochSeconds(data?.CreateTime),
            data?.LineItems?.Select(item => (item.SellerSku, item.Quantity, item.SalePrice, item.SellerDiscount)));
    }

    private sealed record Payload(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("shop_id")] string? ShopId,
        [property: JsonPropertyName("data")] Data? Data);

    private sealed record Data(
        [property: JsonPropertyName("order_id")] string? OrderId,
        [property: JsonPropertyName("create_time")] long? CreateTime,
        [property: JsonPropertyName("line_items")] IReadOnlyList<LineItem>? LineItems);

    private sealed record LineItem(
        [property: JsonPropertyName("seller_sku")] string? SellerSku,
        [property: JsonPropertyName("quantity")] int? Quantity,
        [property: JsonPropertyName("sale_price")] decimal? SalePrice,
        [property: JsonPropertyName("seller_discount")] decimal? SellerDiscount);
}
