using System.Text.Json;
using Oism.Channel.Domain;

namespace Oism.Channel.Application.Webhooks.Adapters;

// Hai trường phải đọc được trước khi biết webhook thuộc tenant nào: mã sự kiện để chống trùng, mã shop để tìm ChannelShop.
public sealed record WebhookEnvelope(string EventId, string ShopId);

// Adapter của một sàn: đổi payload riêng của sàn về Canonical Order (UC-SIM-01 AC-7), và dựng payload ngược lại cho simulator.
// Hình dạng payload của từng sàn: docs/design/api/channel.md mục "Hình dạng payload giả lập".
public abstract class ChannelAdapter
{
    private static readonly ChannelAdapter[] All = [new ShopeeAdapter(), new TikTokAdapter(), new LazadaAdapter()];

    public abstract SalesChannel Channel { get; }

    protected abstract string EventIdField { get; }

    protected abstract string ShopIdField { get; }

    public static ChannelAdapter For(SalesChannel channel) => All.Single(adapter => adapter.Channel == channel);

    // Null khi payload không phải object hoặc thiếu một trong hai trường.
    public WebhookEnvelope? ReadEnvelope(JsonElement payload) =>
        payload.ValueKind == JsonValueKind.Object && Text(payload, EventIdField) is { } eventId && Text(payload, ShopIdField) is { } shopId
            ? new WebhookEnvelope(eventId, shopId)
            : null;

    // Ném InvalidPayloadException khi payload thiếu trường hoặc sai kiểu.
    public CanonicalOrder ToCanonical(JsonElement payload)
    {
        try
        {
            return Convert(payload);
        }
        catch (JsonException exception)
        {
            throw new InvalidPayloadException($"Payload sai kiểu dữ liệu ở {exception.Path ?? "một trường"}");
        }
    }

    // Payload theo hình dạng của sàn, cho công cụ bắn tải.
    public abstract string Build(WebhookEnvelope envelope, CanonicalOrder order);

    protected abstract CanonicalOrder Convert(JsonElement payload);

    // Giây epoch của Shopee và TikTok; null khi thiếu hoặc nằm ngoài khoảng DateTimeOffset biểu diễn được.
    protected static DateTimeOffset? FromEpochSeconds(long? seconds) =>
        seconds is >= -62_135_596_800 and <= 253_402_300_799 ? DateTimeOffset.FromUnixTimeSeconds(seconds.Value) : null;

    private static string? Text(JsonElement payload, string field) =>
        payload.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;
}
