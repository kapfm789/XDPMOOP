using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Oism.BuildingBlocks.Auth;
using Oism.BuildingBlocks.Tenancy;

namespace Oism.Insights.Api.Hubs;

// UC-SIM-02: hub /hubs/notifications. Client chỉ nghe; server tự đưa kết nối vào group theo token
// (docs/design/api/insights.md mục "Thông báo realtime"). Không token hợp lệ thì bị từ chối (AC-5).
[Authorize(Policy = Policies.AnyRole)]
public sealed class NotificationsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (NotificationGroups.Of(Context.User!) is { } group)
            await Groups.AddToGroupAsync(Context.ConnectionId, group);
        await base.OnConnectedAsync();
    }
}

public static class NotificationGroups
{
    public static string Tenant(Guid tenantId) => $"tenant:{tenantId}";

    public static string Branch(Guid tenantId, Guid branchId) => $"tenant:{tenantId}:branch:{branchId}";

    // Owner và Staff nhận thông báo của mọi chi nhánh trong tenant; người dùng khác gắn với một chi nhánh chỉ nhận của
    // chi nhánh đó (UC-SIM-02 AC-2). Mỗi kết nối vào đúng một group, nên một thông báo gửi cho cả hai group không tới
    // ai hai lần. Null khi token không đủ để chọn group: kết nối không nhận gì.
    public static string? Of(ClaimsPrincipal user)
    {
        if (!Guid.TryParse(user.FindFirstValue(TenantMiddleware.TenantIdClaim), out var tenantId))
            return null;
        if (user.IsInRole(Roles.Owner) || user.IsInRole(Roles.Staff))
            return Tenant(tenantId);
        return Guid.TryParse(user.FindFirstValue("branch_id"), out var branchId) ? Branch(tenantId, branchId) : null;
    }
}
