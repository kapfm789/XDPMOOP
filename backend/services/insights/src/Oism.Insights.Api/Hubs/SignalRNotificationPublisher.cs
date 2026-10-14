using Microsoft.AspNetCore.SignalR;
using Oism.BuildingBlocks.Messaging;
using Oism.BuildingBlocks.Tenancy;
using Oism.Insights.Application.Notifications;

namespace Oism.Insights.Api.Hubs;

// Scoped: giữ thông báo của thông điệp đang xử lý, gửi khi consumer base đã commit (IAfterCommitAction).
// Chỉ gửi vào group của tenant hiện tại (docs/architecture/multi-tenancy.md quy tắc 6).
internal sealed class SignalRNotificationPublisher(IHubContext<NotificationsHub> hub, ITenantContext tenant)
    : INotificationPublisher, IAfterCommitAction
{
    private readonly List<Func<CancellationToken, Task>> _pending = [];

    private Guid TenantId => tenant.TenantId ?? throw new InvalidOperationException("Cannot notify without a tenant context.");

    public void OrderCreated(OrderCreatedNotification notification)
    {
        var groups = new[] { NotificationGroups.Tenant(TenantId), NotificationGroups.Branch(TenantId, notification.BranchId) };
        _pending.Add(ct => hub.Clients.Groups(groups).SendAsync(nameof(OrderCreated), notification, ct));
    }

    public void LowStock(LowStockNotification notification)
    {
        var group = NotificationGroups.Tenant(TenantId);
        _pending.Add(ct => hub.Clients.Group(group).SendAsync(nameof(LowStock), notification, ct));
    }

    public async Task RunAsync(CancellationToken ct)
    {
        foreach (var send in _pending)
            await send(ct);
        _pending.Clear();
    }
}
