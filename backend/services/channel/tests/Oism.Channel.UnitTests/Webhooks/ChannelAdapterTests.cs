using System.Text.Json;
using Oism.Channel.Application.Webhooks.Adapters;
using Oism.Channel.Domain;

namespace Oism.Channel.UnitTests.Webhooks;

// Ánh xạ payload của từng sàn về Canonical Order: docs/design/api/channel.md mục "Ánh xạ về Canonical Order".
// Ba payload dưới đây là ba ví dụ của tài liệu đó.
public sealed class ChannelAdapterTests
{
    private const string Shopee = """
        {
          "event_id": "sp-000123",
          "shop_id": "shopee-shop-01",
          "ordersn": "SP2610200001",
          "create_time": 1792466127,
          "item_list": [{ "model_sku": "AO-A-DEN-M", "quantity": 1, "price": 150000, "discount": 0 }]
        }
        """;

    private const string TikTok = """
        {
          "id": "tt-evt-000123",
          "shop_id": "tiktok-shop-01",
          "data": {
            "order_id": "TT2610200001",
            "create_time": 1792466127,
            "line_items": [{ "seller_sku": "AO-A-DEN-M", "quantity": 1, "sale_price": 150000, "seller_discount": 0 }]
          }
        }
        """;

    private const string Lazada = """
        {
          "message_id": "lz-000123",
          "seller_id": "lazada-seller-01",
          "order": {
            "order_number": "LZ2610200001",
            "created_at": "2026-10-20T03:15:27Z",
            "items": [{ "sku": "AO-A-DEN-M", "qty": 1, "paid_price": 150000, "voucher": 0 }]
          }
        }
        """;

    private static readonly DateTimeOffset PlacedAt = new(2026, 10, 20, 3, 15, 27, TimeSpan.Zero);

    [Theory]
    [InlineData(SalesChannel.Shopee, Shopee, "sp-000123", "shopee-shop-01", "SP2610200001")]
    [InlineData(SalesChannel.TikTok, TikTok, "tt-evt-000123", "tiktok-shop-01", "TT2610200001")]
    [InlineData(SalesChannel.Lazada, Lazada, "lz-000123", "lazada-seller-01", "LZ2610200001")]
    [Trait("UseCase", "UC-SIM-01 AC-7")]
    [Trait("UseCase", "UC-ORD-01 AC-7")]
    public void ToCanonical_DocumentedPayloadOfEachChannel_GivesTheSameCanonicalOrder(
        SalesChannel channel, string json, string eventId, string shopId, string externalOrderId)
    {
        var adapter = ChannelAdapter.For(channel);
        var payload = JsonDocument.Parse(json).RootElement;

        var envelope = adapter.ReadEnvelope(payload);
        var order = adapter.ToCanonical(payload);

        Assert.Equal(new WebhookEnvelope(eventId, shopId), envelope);
        Assert.Equal((externalOrderId, PlacedAt), (order.ExternalOrderId, order.PlacedAt));
        Assert.Equal(new CanonicalOrderLine("AO-A-DEN-M", 1, 150_000, 0), Assert.Single(order.Lines));
    }

    // Simulator dựng payload bằng chính adapter: payload dựng ra phải đọc lại được về đúng đơn đã cho.
    [Theory]
    [InlineData(SalesChannel.Shopee)]
    [InlineData(SalesChannel.TikTok)]
    [InlineData(SalesChannel.Lazada)]
    [Trait("UseCase", "UC-SIM-01 AC-5")]
    public void Build_ThenToCanonical_RoundTripsTheOrderAndTheEnvelope(SalesChannel channel)
    {
        var adapter = ChannelAdapter.For(channel);
        var envelope = new WebhookEnvelope("sim-1", "shop-1");
        var order = CanonicalOrder.From("SIM-0001", PlacedAt, [("AO-01", 2, 150_000m, 10_000m), ("QUAN-01", 1, 99_500.5m, 0m)]);

        var payload = JsonDocument.Parse(adapter.Build(envelope, order)).RootElement;
        var readBack = adapter.ToCanonical(payload);

        Assert.Equal(envelope, adapter.ReadEnvelope(payload));
        Assert.Equal((order.ExternalOrderId, order.PlacedAt), (readBack.ExternalOrderId, readBack.PlacedAt));
        Assert.Equal(order.Lines, readBack.Lines);
    }

    [Theory]
    [InlineData("""{"event_id":"e","shop_id":"s","create_time":1792466127,"item_list":[{"model_sku":"A","quantity":1,"price":1}]}""", "Thiếu mã đơn")]
    [InlineData("""{"event_id":"e","shop_id":"s","ordersn":"O","item_list":[{"model_sku":"A","quantity":1,"price":1}]}""", "Thiếu thời điểm đặt hàng")]
    [InlineData("""{"event_id":"e","shop_id":"s","ordersn":"O","create_time":1792466127}""", "Đơn không có dòng hàng")]
    [InlineData("""{"event_id":"e","shop_id":"s","ordersn":"O","create_time":1792466127,"item_list":[{"quantity":1,"price":1}]}""", "Dòng 1: thiếu mã SKU")]
    [InlineData("""{"event_id":"e","shop_id":"s","ordersn":"O","create_time":1792466127,"item_list":[{"model_sku":"A","quantity":0,"price":1}]}""", "Dòng 1: số lượng phải dương")]
    [InlineData("""{"event_id":"e","shop_id":"s","ordersn":"O","create_time":1792466127,"item_list":[{"model_sku":"A","quantity":1}]}""", "Dòng 1: thiếu đơn giá hoặc đơn giá âm")]
    [InlineData("""{"event_id":"e","shop_id":"s","ordersn":"O","create_time":1792466127,"item_list":[{"model_sku":"A","quantity":1,"price":10},{"model_sku":"B","quantity":2,"price":10,"discount":21}]}""", "Dòng 2: giảm giá âm hoặc vượt thành tiền của dòng")]
    public void ToCanonical_MissingOrInvalidValue_ThrowsInvalidPayloadWithTheReason(string json, string reason)
    {
        var failure = Assert.Throws<InvalidPayloadException>(() =>
            ChannelAdapter.For(SalesChannel.Shopee).ToCanonical(JsonDocument.Parse(json).RootElement));

        Assert.Equal(reason, failure.Message);
    }

    [Theory]
    [InlineData(SalesChannel.Shopee, """{"event_id":"e","shop_id":"s","ordersn":"O","create_time":"hôm qua","item_list":[]}""")]
    [InlineData(SalesChannel.TikTok, """{"id":"e","shop_id":"s","data":{"order_id":"O","create_time":1792466127,"line_items":[{"seller_sku":"A","quantity":1.5,"sale_price":1}]}}""")]
    [InlineData(SalesChannel.Lazada, """{"message_id":"e","seller_id":"s","order":"không phải object"}""")]
    public void ToCanonical_FieldOfTheWrongType_ThrowsInvalidPayloadInsteadOfAJsonError(SalesChannel channel, string json)
    {
        var failure = Assert.Throws<InvalidPayloadException>(() =>
            ChannelAdapter.For(channel).ToCanonical(JsonDocument.Parse(json).RootElement));

        Assert.StartsWith("Payload sai kiểu dữ liệu", failure.Message);
    }

    [Theory]
    [InlineData("""[1, 2]""")]
    [InlineData("""{"shop_id":"s"}""")]
    [InlineData("""{"event_id":"e"}""")]
    [InlineData("""{"event_id":" ","shop_id":"s"}""")]
    [InlineData("""{"event_id":123,"shop_id":"s"}""")]
    public void ReadEnvelope_WithoutEventIdOrShopId_ReturnsNull(string json) =>
        Assert.Null(ChannelAdapter.For(SalesChannel.Shopee).ReadEnvelope(JsonDocument.Parse(json).RootElement));
}
