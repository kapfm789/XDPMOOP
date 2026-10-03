using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Oism.BuildingBlocks.Tenancy;

public static class TenantMiddleware
{
    public const string TenantIdClaim = "tenant_id";

    // Đặt sau UseAuthentication: chỉ đọc claim từ JWT đã kiểm.
    public static IApplicationBuilder UseTenantContext(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (Guid.TryParse(context.User.FindFirstValue(TenantIdClaim), out var tenantId))
                context.RequestServices.GetRequiredService<ITenantContext>().Set(tenantId);

            await next(context);
        });
}
