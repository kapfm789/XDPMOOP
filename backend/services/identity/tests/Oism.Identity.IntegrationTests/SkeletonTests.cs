using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Oism.Identity.Domain;
using Oism.Identity.Infrastructure;
using Oism.SharedKernel;

namespace Oism.Identity.IntegrationTests;

public sealed class SkeletonTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_ServiceStarted_Returns200()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // docs/architecture/multi-tenancy.md: entity không có tenant_id (bảng tenants) phải được loại trừ tường minh ở đây.
    [Fact]
    public void DbContext_EveryEntity_ImplementsITenantOwned()
    {
        using var scope = factory.Services.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Model;

        Assert.DoesNotContain(model.GetEntityTypes(), entity =>
            entity.ClrType != typeof(Tenant) && !typeof(ITenantOwned).IsAssignableFrom(entity.ClrType));
    }
}
