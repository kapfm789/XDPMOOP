using System.Net;
using System.Net.Http.Json;
using Oism.BuildingBlocks.Auth;
using Oism.Catalog.Application.Products;
using static Oism.Catalog.IntegrationTests.ApiExtensions;

namespace Oism.Catalog.IntegrationTests.Products;

// UC-PROD-04: giá lẻ, giá sỉ.
public sealed class PriceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private HttpClient Owner => factory.CreateClient(Roles.Owner, _tenantId);

    private HttpClient Staff => factory.CreateClient(Roles.Staff, _tenantId);

    [Fact]
    [Trait("UseCase", "UC-PROD-04 AC-1")]
    public async Task SetPrices_OwnerWithNonNegativePrices_SkuCarriesTheNewPrices()
    {
        var sku = await Owner.CreateSkuAsync("BUT-01");

        var response = await Owner.PutAsJsonAsync($"/skus/{sku.Id}/prices", new { retailPrice = 5_500.5m, wholesalePrice = 0 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<SkuDto>())!;
        Assert.Equal((5_500.5m, 0m), (updated.RetailPrice, updated.WholesalePrice));
        var stored = Assert.Single((await Staff.GetProductAsync(sku.ProductId)).Skus);
        Assert.Equal((5_500.5m, 0m), (stored.RetailPrice, stored.WholesalePrice));
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-04 AC-2")]
    public async Task SetPrices_NegativePrice_Returns400AndKeepsThePrices()
    {
        var product = await Owner.CreateProductAsync("Bút bi", hasVariants: false, Sku("BUT-01", retailPrice: 5_000, wholesalePrice: 4_000));
        var sku = Assert.Single(product.Skus);

        var negativeRetail = await Owner.PutAsJsonAsync($"/skus/{sku.Id}/prices", new { retailPrice = -1, wholesalePrice = 4_000 });
        var negativeWholesale = await Owner.PutAsJsonAsync($"/skus/{sku.Id}/prices", new { retailPrice = 5_000, wholesalePrice = -0.01m });
        var negativeOnCreate = await Owner.PostProductAsync("Vở", product.CategoryId, hasVariants: false, Sku("VO-01", retailPrice: -1));
        var unknownSku = await Owner.PutAsJsonAsync($"/skus/{Guid.NewGuid()}/prices", new { retailPrice = 1, wholesalePrice = 1 });

        await negativeRetail.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await negativeWholesale.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await negativeOnCreate.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await unknownSku.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        var stored = Assert.Single((await Owner.GetProductAsync(product.Id)).Skus);
        Assert.Equal((5_000m, 4_000m), (stored.RetailPrice, stored.WholesalePrice));
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-04 AC-3")]
    [Trait("UseCase", "UC-AUTH-03 AC-4")]
    public async Task Prices_Staff_CannotSetThemAndPricesSentOnCreateAreIgnored()
    {
        var product = await Staff.CreateProductAsync(
            "Áo thun", hasVariants: true, Sku("AO-01", retailPrice: 150_000, wholesalePrice: 120_000));
        var created = Assert.Single(product.Skus);

        var setPrices = await Staff.PutAsJsonAsync($"/skus/{created.Id}/prices", new { retailPrice = 150_000, wholesalePrice = 120_000 });
        var added = await Staff.PostAsJsonAsync($"/products/{product.Id}/skus", Sku("AO-02", retailPrice: 99_000));

        await setPrices.AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        Assert.All((await Staff.GetProductAsync(product.Id)).Skus, sku => Assert.Equal((0m, 0m), (sku.RetailPrice, sku.WholesalePrice)));
        Assert.All(await factory.SkuEventsAsync(_tenantId), message => Assert.Equal((0m, 0m), (message.RetailPrice, message.WholesalePrice)));
    }
}
