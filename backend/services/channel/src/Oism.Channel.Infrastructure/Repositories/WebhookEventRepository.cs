using Microsoft.EntityFrameworkCore;
using Oism.Channel.Application;
using Oism.Channel.Application.Webhooks;
using Oism.Channel.Domain;

namespace Oism.Channel.Infrastructure.Repositories;

internal sealed class WebhookEventRepository(ChannelDbContext db) : IWebhookEventRepository
{
    public Task<WebhookEvent?> FindAsync(SalesChannel channel, string eventId, CancellationToken ct) =>
        db.WebhookEvents.AsNoTracking().SingleOrDefaultAsync(webhook => webhook.Channel == channel && webhook.EventId == eventId, ct);

    public async Task<IReadOnlyList<WebhookEvent>> ListSubmittedAsync(SalesChannel channel, string externalOrderId, CancellationToken ct) =>
        await db.WebhookEvents
            .Where(webhook => webhook.Channel == channel && webhook.ExternalOrderId == externalOrderId
                && webhook.Status == WebhookStatus.Submitted)
            .ToListAsync(ct);

    public Task<Guid?> FindReservedOrderIdAsync(SalesChannel channel, string externalOrderId, CancellationToken ct) =>
        db.WebhookEvents.AsNoTracking()
            .Where(webhook => webhook.Channel == channel && webhook.ExternalOrderId == externalOrderId
                && webhook.Status == WebhookStatus.Reserved)
            .Select(webhook => webhook.OrderId)
            .FirstOrDefaultAsync(ct);

    public async Task<PagedResult<WebhookEventDto>> ListAsync(
        SalesChannel? channel, WebhookStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var webhooks = db.WebhookEvents.AsNoTracking()
            .Where(webhook => (channel == null || webhook.Channel == channel) && (status == null || webhook.Status == status));

        var items = await webhooks
            .OrderByDescending(webhook => webhook.ReceivedAt).ThenBy(webhook => webhook.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(webhook => new WebhookEventDto(
                webhook.Id, webhook.Channel.ToString(), webhook.EventId, webhook.ExternalOrderId, webhook.Status.ToString(),
                webhook.Reason, webhook.OrderId, webhook.ReceivedAt, webhook.UpdatedAt))
            .ToListAsync(ct);
        return new PagedResult<WebhookEventDto>(items, page, pageSize, await webhooks.CountAsync(ct));
    }

    public void Add(WebhookEvent webhook) => db.Add(webhook);
}
