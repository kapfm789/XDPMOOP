namespace Oism.Channel.Domain;

// Ba sàn giả lập (ADR-0006). Tên của từng giá trị là giá trị `channel` trong SubmitOrder (docs/design/events.md).
public enum SalesChannel
{
    Shopee,
    TikTok,
    Lazada,
}

public static class SalesChannels
{
    // Nhận tên kênh không phân biệt hoa thường, như `shopee` trên đường dẫn webhook. Không nhận số.
    public static bool TryParse(string? value, out SalesChannel channel)
    {
        foreach (var candidate in Enum.GetValues<SalesChannel>())
        {
            if (string.Equals(candidate.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                channel = candidate;
                return true;
            }
        }

        channel = default;
        return false;
    }
}
