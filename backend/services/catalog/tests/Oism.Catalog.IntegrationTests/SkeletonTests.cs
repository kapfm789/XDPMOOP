using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
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

    // T21: Swagger sinh từ code ghi vai trò và yêu cầu Bearer khớp cột "Vai trò" của docs/design/api/.
    [Fact]
    [Trait("Scenario", "T21")]
    public async Task Swagger_Document_MarksRolesAndPublicEndpoints()
    {
        var paths = (await factory.CreateClient().GetFromJsonAsync<JsonObject>("/swagger/v1/swagger.json"))!["paths"]!;

        Assert.Equal("Vai trò: Owner, Staff.", (string?)paths["/brands"]!["get"]!["description"]);
        Assert.NotNull(paths["/brands"]!["get"]!["security"]);
    }

    // docs/architecture/multi-tenancy.md: entity không có tenant_id (bảng tenants) phải được loại trừ tường minh ở đây.
    [Fact]
    [Trait("Scenario", "T14")]
    public void DbContext_EveryEntity_ImplementsITenantOwned()
    {
        using var scope = factory.Services.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Model;

        Assert.DoesNotContain(model.GetEntityTypes(), entity => !typeof(ITenantOwned).IsAssignableFrom(entity.ClrType));
    }
}
