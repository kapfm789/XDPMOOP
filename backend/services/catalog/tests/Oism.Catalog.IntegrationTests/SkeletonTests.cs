using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Oism.Catalog.Infrastructure;
using Oism.SharedKernel;

namespace Oism.Catalog.IntegrationTests;

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
        var model = scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Model;

        Assert.DoesNotContain(model.GetEntityTypes(), entity => !typeof(ITenantOwned).IsAssignableFrom(entity.ClrType));
    }
}
