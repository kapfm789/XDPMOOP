using Oism.Channel.Domain;
using Oism.Contracts;

namespace Oism.Channel.Application.Webhooks.ApplyOrderResult;

// Kết quả giữ hàng của `core` về tới `channel` bằng event (docs/design/flows/webhook-ingestion.md).
// Gọi từ consumer: phần chung đã mở transaction, ghi inbox và sẽ lưu rồi commit, nên handler này không tự mở transaction.
public sealed class ApplyOrderResultHandler(IWebhookEventRepository webhooks, IClock clock)
{
    public async Task Handle(OrderReserved message, CancellationToken ct)
    {
        foreach (var webhook in await ListSubmittedAsync(message.Channel, message.ExternalOrderId, ct))
            webhook.MarkReserved(message.OrderId, clock.UtcNow);
    }

    public async Task Handle(OrderRejected message, CancellationToken ct)
    {
        foreach (var webhook in await ListSubmittedAsync(message.Channel, message.ExternalOrderId, ct))
            webhook.MarkRejected(message.Reason, clock.UtcNow);
    }

    // OrderReserved cũng phát cho đơn thủ công, không có mã đơn của sàn: không có bản ghi webhook nào để đổi.
    // Thông điệp tới lại sau khi bản ghi đã rời Submitted cũng không đổi gì.
    private async Task<IReadOnlyList<WebhookEvent>> ListSubmittedAsync(string channel, string? externalOrderId, CancellationToken ct) =>
        externalOrderId is not null && SalesChannels.TryParse(channel, out var parsed)
            ? await webhooks.ListSubmittedAsync(parsed, externalOrderId, ct)
            : [];
}
