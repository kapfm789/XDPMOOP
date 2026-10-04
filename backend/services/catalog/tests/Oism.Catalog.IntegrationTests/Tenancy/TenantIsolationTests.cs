using System.Net;
using System.Net.Http.Json;
using Oism.BuildingBlocks.Auth;

namespace Oism.Catalog.IntegrationTests.Tenancy;

// T14 cho danh mục và thương hiệu: tenant A dùng ID của tenant B nhận 404, dữ liệu của B không đổi.
public sealed class TenantIsolationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _tenantA = factory.CreateClient(Roles.Owner, Guid.NewGuid());
    private readonly HttpClient _tenantB = factory.CreateClient(Roles.Owner, Guid.NewGuid());

    [Fact]
    [Trait("Scenario", "T14")]
    public async Task Categories_IdOfAnotherTenant_Returns404AndLeavesItUnchanged()
    {
        var categoryOfB = await _tenantB.CreateCategoryAsync("Danh mục của B");
        await _tenantB.CreateCategoryAsync("Con của B", categoryOfB.Id);

        var update = await _tenantA.PutAsJsonAsync($"/categories/{categoryOfB.Id}", new { name = "Bị đổi" });
        var delete = await _tenantA.DeleteAsync($"/categories/{categoryOfB.Id}");
        var attach = await _tenantA.PostAsJsonAsync("/categories", new { name = "Con của A", parentId = categoryOfB.Id });

        await update.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await delete.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await attach.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        Assert.Empty(await _tenantA.GetCategoryTreeAsync());
        var treeOfB = Assert.Single(await _tenantB.GetCategoryTreeAsync());
        Assert.Equal("Danh mục của B", treeOfB.Name);
        Assert.Equal("Con của B", Assert.Single(treeOfB.Children).Name);
    }

    [Fact]
    [Trait("Scenario", "T14")]
    public async Task Brands_IdOfAnotherTenant_Returns404AndLeavesItUnchanged()
    {
        var brandOfB = await _tenantB.CreateBrandAsync("Thương hiệu của B");

        var update = await _tenantA.PutAsJsonAsync($"/brands/{brandOfB.Id}", new { name = "Bị đổi" });

        await update.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        Assert.Empty(await _tenantA.GetBrandsAsync());
        Assert.Equal([brandOfB], await _tenantB.GetBrandsAsync());
    }
}
