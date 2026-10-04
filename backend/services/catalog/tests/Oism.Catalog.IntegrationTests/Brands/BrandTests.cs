using System.Net;
using System.Net.Http.Json;
using Oism.BuildingBlocks.Auth;
using Oism.Catalog.Application.Brands;

namespace Oism.Catalog.IntegrationTests.Brands;

// UC-PROD-01: thương hiệu.
public sealed class BrandTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _staff = factory.CreateClient(Roles.Staff, Guid.NewGuid());

    [Fact]
    [Trait("UseCase", "UC-PROD-01 AC-4")]
    public async Task UpsertBrand_CreateThenRename_ListShowsNewName()
    {
        var brand = await _staff.CreateBrandAsync("  Nikee ");
        await _staff.CreateBrandAsync("Adidas");

        var response = await _staff.PutAsJsonAsync($"/brands/{brand.Id}", new { name = "Nike" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Nikee", brand.Name);
        Assert.Equal(new BrandDto(brand.Id, "Nike"), await response.Content.ReadFromJsonAsync<BrandDto>());
        Assert.Equal(["Adidas", "Nike"], (await _staff.GetBrandsAsync()).Select(item => item.Name));
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-01 AC-4")]
    public async Task UpsertBrand_NameAlreadyInTenant_Returns409()
    {
        var puma = await _staff.CreateBrandAsync("Puma");
        await _staff.CreateBrandAsync("Vans");

        var create = await _staff.PostAsJsonAsync("/brands", new { name = "Vans" });
        var rename = await _staff.PutAsJsonAsync($"/brands/{puma.Id}", new { name = "Vans" });

        await create.AssertProblemAsync(HttpStatusCode.Conflict, "duplicate");
        await rename.AssertProblemAsync(HttpStatusCode.Conflict, "duplicate");
        Assert.Equal(["Puma", "Vans"], (await _staff.GetBrandsAsync()).Select(item => item.Name));
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-01 AC-4")]
    public async Task UpsertBrand_SameNameInAnotherTenant_Returns201()
    {
        await _staff.CreateBrandAsync("Uniqlo");
        var otherTenant = factory.CreateClient(Roles.Owner, Guid.NewGuid());

        var brand = await otherTenant.CreateBrandAsync("Uniqlo");

        Assert.Equal([brand], await otherTenant.GetBrandsAsync());
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-01 AC-4")]
    public async Task UpsertBrand_BlankNameOrUnknownId_Returns400Or404()
    {
        var blank = await _staff.PostAsJsonAsync("/brands", new { name = " " });
        var unknown = await _staff.PutAsJsonAsync($"/brands/{Guid.NewGuid()}", new { name = "Không có" });

        await blank.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await unknown.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-03 AC-3")]
    public async Task Brands_Cashier_Returns403()
    {
        var cashier = factory.CreateClient(Roles.Cashier, Guid.NewGuid());

        var read = await cashier.GetAsync("/brands");
        var write = await cashier.PostAsJsonAsync("/brands", new { name = "Bị chặn" });

        await read.AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        await write.AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
    }
}
