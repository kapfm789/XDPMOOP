using Oism.SharedKernel;

namespace Oism.Channel.Domain;

public enum WebhookStatus
{
    Received,
    Submitted,
    Reserved,
    Rejected,
    Failed,
}

// Bước chuyển không có trong bảng ở docs/design/state-machines.md.
public sealed class InvalidStateTransitionException(string subject, string from, string to)
    : OismException("invalid_state_transition", $"Không thể chuyển {subject} từ {from} sang {to}", new { from, to });

// Một webhook đã nhận: docs/design/data-model/channel.md mục "webhook_events";
// trạng thái ở docs/design/state-machines.md mục "Bản ghi webhook".
public sealed class WebhookEvent : ITenantOwned
{
    private WebhookEvent()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public SalesChannel Channel { get; private set; }

    // Mã sự kiện do sàn cấp; unique theo tenant và kênh, là hàng rào chống trùng đầu tiên (ADR-0006).
    public string EventId { get; private set; } = null!;

    // Null khi payload không đổi được về Canonical Order.
    public string? ExternalOrderId { get; private set; }

    // Nguyên văn body đã nhận.
    public string Payload { get; private set; } = null!;

    public WebhookStatus Status { get; private set; }

    // Lý do khi Rejected hoặc Failed.
    public string? Reason { get; private set; }

    // Đơn ở `core` khi Reserved.
    public Guid? OrderId { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static WebhookEvent Receive(SalesChannel channel, string eventId, string payload, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        Channel = channel,
        EventId = eventId,
        Payload = payload,
        Status = WebhookStatus.Received,
        ReceivedAt = now,
        UpdatedAt = now,
    };

    // Đã ghi SubmitOrder vào outbox trong cùng transaction.
    public void MarkSubmitted(string externalOrderId, DateTimeOffset now)
    {
        MoveTo(WebhookStatus.Received, WebhookStatus.Submitted, now);
        ExternalOrderId = externalOrderId;
    }

    // Payload không đổi được về Canonical Order: không phát command.
    public void MarkFailed(string reason, DateTimeOffset now)
    {
        MoveTo(WebhookStatus.Received, WebhookStatus.Failed, now);
        Reason = reason;
    }

    // `core` đã giữ được hàng cho đơn.
    public void MarkReserved(Guid orderId, DateTimeOffset now)
    {
        MoveTo(WebhookStatus.Submitted, WebhookStatus.Reserved, now);
        OrderId = orderId;
    }

    // `core` từ chối đơn.
    public void MarkRejected(string reason, DateTimeOffset now)
    {
        MoveTo(WebhookStatus.Submitted, WebhookStatus.Rejected, now);
        Reason = reason;
    }

    private void MoveTo(WebhookStatus from, WebhookStatus to, DateTimeOffset now)
    {
        if (Status != from)
            throw new InvalidStateTransitionException("bản ghi webhook", Status.ToString(), to.ToString());

        Status = to;
        UpdatedAt = now;
    }
}
