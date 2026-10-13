using Oism.Channel.Domain;

namespace Oism.Channel.Application.Webhooks;

// Một webhook đã nhận: docs/design/api/channel.md. Payload không nằm trong danh sách.
public sealed record WebhookEventDto(
    Guid Id,
    string Channel,
    string EventId,
    string? ExternalOrderId,
    string Status,
    string? Reason,
    Guid? OrderId,
    DateTimeOffset ReceivedAt,
    DateTimeOffset UpdatedAt);

public interface IWebhookEventRepository
{
    // Bản ghi của tenant hiện tại theo kênh và mã sự kiện. Chỉ để đọc.
    Task<WebhookEvent?> FindAsync(SalesChannel channel, string eventId, CancellationToken ct);

    // Các bản ghi còn Submitted của một đơn của sàn, có theo dõi. Thường là một; hai webhook khác mã sự kiện
    // mà cùng một đơn thì là hai.
    Task<IReadOnlyList<WebhookEvent>> ListSubmittedAsync(SalesChannel channel, string externalOrderId, CancellationToken ct);

    // Đơn ở `core` của một bản ghi đã Reserved cho cùng mã đơn của sàn; null khi chưa có bản ghi nào như vậy.
    Task<Guid?> FindReservedOrderIdAsync(SalesChannel channel, string externalOrderId, CancellationToken ct);

    // Mới nhận nhất trước.
    Task<PagedResult<WebhookEventDto>> ListAsync(
        SalesChannel? channel, WebhookStatus? status, int page, int pageSize, CancellationToken ct);

    void Add(WebhookEvent webhook);
}
