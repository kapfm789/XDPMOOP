using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Oism.BuildingBlocks.Auth;
using Oism.BuildingBlocks.Tenancy;

namespace Oism.Insights.Api.Hubs;

// UC-SIM-02: hub /hubs/notifications. Server tự đưa kết nối vào group theo token
// (docs/design/api/insights.md mục "Thông báo realtime"). Không token hợp lệ thì bị từ chối (AC-5).
[Authorize(Policy = Policies.AnyRole)]
public sealed class NotificationsHub : Hub
{
    public const int MaxScanLength = 64;

    public override async Task OnConnectedAsync()
    {
        if (NotificationGroups.Of(Context.User!) is { } group)
            await Groups.AddToGroupAsync(Context.ConnectionId, group);
        if (NotificationGroups.OwnUser(Context.User!) is { } own)
            await Groups.AddToGroupAsync(Context.ConnectionId, own);
        await base.OnConnectedAsync();
    }

    // W5-15, UC-POS-01 AC-9: điện thoại làm máy quét. Mã chỉ tới các kết nối khác của chính người dùng này;
    // hub không tìm SKU, màn hình bán hàng nhận mã xử lý như máy quét.
    [Authorize(Policy = Policies.OwnerOrCashier)]
    public async Task SendScan(string code)
    {
        var trimmed = code?.Trim() ?? "";
        if (trimmed.Length is 0 or > MaxScanLength)
            throw new HubException("invalid_scan");
        if (NotificationGroups.OwnUser(Context.User!) is not { } group)
            throw new HubException("forbidden");
        await Clients.OthersInGroup(group).SendAsync("RemoteScan", new RemoteScanNotification(trimmed));
    }
}

public sealed record RemoteScanNotification(string Code);

public static class NotificationGroups
{
    public static string Tenant(Guid tenantId) => $"tenant:{tenantId}";

    public static string Branch(Guid tenantId, Guid branchId) => $"tenant:{tenantId}:branch:{branchId}";

    public static string User(Guid tenantId, Guid userId) => $"tenant:{tenantId}:user:{userId}";

    // Owner và Staff nhận thông báo của mọi chi nhánh trong tenant; người dùng khác gắn với một chi nhánh chỉ nhận của
    // chi nhánh đó (UC-SIM-02 AC-2). Mỗi kết nối vào đúng một group thông báo, nên một thông báo gửi cho cả hai group
    // không tới ai hai lần. Null khi token không đủ để chọn group: kết nối không nhận gì.
    public static string? Of(ClaimsPrincipal user)
    {
        if (!Guid.TryParse(user.FindFirstValue(TenantMiddleware.TenantIdClaim), out var tenantId))
            return null;
        if (user.IsInRole(Roles.Owner) || user.IsInRole(Roles.Staff))
            return Tenant(tenantId);
        return Guid.TryParse(user.FindFirstValue("branch_id"), out var branchId) ? Branch(tenantId, branchId) : null;
    }

    // Group riêng của người dùng cho máy quét từ xa; không thông báo nào gửi vào đây. Tenant và người dùng lấy từ token.
    public static string? OwnUser(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(TenantMiddleware.TenantIdClaim), out var tenantId)
        && Guid.TryParse(user.FindFirstValue("sub"), out var userId)
            ? User(tenantId, userId)
            : null;
}
