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
    public async Task Products_IdOfAnotherTenant_Returns404AndLeavesThemUnchanged()
    {
        var (tenantAId, tenantBId) = (Guid.NewGuid(), Guid.NewGuid());
        var (ownerA, ownerB) = (factory.CreateClient(Roles.Owner, tenantAId), factory.CreateClient(Roles.Owner, tenantBId));
        var brandOfB = await ownerB.CreateBrandAsync("Thương hiệu của B");
        var productOfB = await ownerB.CreateProductAsync(
            "Sản phẩm của B", hasVariants: true, ApiExtensions.Sku("B-01", retailPrice: 10_000, wholesalePrice: 8_000));
        var skuOfB = Assert.Single(productOfB.Skus);
        var barcodeOfB = await ownerB.AddBarcodeAsync(skuOfB.Id, "Code128", "BCODE1");
        var categoryOfA = await ownerA.CreateCategoryAsync("Danh mục của A");
        var skuOfA = await ownerA.CreateSkuAsync("A-01");
        var eventsOfBBefore = (await factory.SkuEventsAsync(tenantBId)).Count;
        var eventsOfABefore = (await factory.SkuEventsAsync(tenantAId)).Count;

        HttpResponseMessage[] responses =
        [
            await ownerA.GetAsync($"/products/{productOfB.Id}"),
            await ownerA.PutAsJsonAsync($"/products/{productOfB.Id}", new { name = "Bị đổi", categoryId = categoryOfA.Id, isActive = false }),
            await ownerA.PostAsJsonAsync($"/products/{productOfB.Id}/skus", ApiExtensions.Sku("A-02")),
            await ownerA.PutAsJsonAsync($"/skus/{skuOfB.Id}", new { isActive = false }),
            await ownerA.PutAsJsonAsync($"/skus/{skuOfB.Id}/prices", new { retailPrice = 1, wholesalePrice = 1 }),
            await ownerA.PostBarcodeAsync(skuOfB.Id, "EAN13"),
            await ownerA.DeleteAsync($"/skus/{skuOfB.Id}/barcodes/{barcodeOfB.Id}"),
            // Mã vạch của B không xóa được qua SKU của A.
            await ownerA.DeleteAsync($"/skus/{skuOfA.Id}/barcodes/{barcodeOfB.Id}"),
            // Gắn sản phẩm của A vào danh mục hoặc thương hiệu của B: không quan hệ chéo nào được tạo.
            await ownerA.PostProductAsync("Sản phẩm chéo", productOfB.CategoryId, hasVariants: false, ApiExtensions.Sku("A-03")),
            await ownerA.PostAsJsonAsync("/products", new
            {
                name = "Sản phẩm chéo", categoryId = categoryOfA.Id, brandId = brandOfB.Id, hasVariants = false,
                skus = new[] { ApiExtensions.Sku("A-04") },
            }),
            await ownerA.PutAsJsonAsync($"/products/{skuOfA.ProductId}", new { name = "Chuyển sang B", categoryId = productOfB.CategoryId, isActive = true }),
        ];

        foreach (var response in responses)
            await response.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        Assert.Equal([skuOfA.ProductId], (await ownerA.ListProductsAsync()).Items.Select(item => item.Id));
        Assert.Equal(0, (await ownerA.ListProductsAsync($"?categoryId={productOfB.CategoryId}")).Total);

        var unchanged = await ownerB.GetProductAsync(productOfB.Id);
        var unchangedSku = Assert.Single(unchanged.Skus);
        Assert.Equal(("Sản phẩm của B", productOfB.CategoryId, true), (unchanged.Name, unchanged.CategoryId, unchanged.IsActive));
        Assert.Equal((skuOfB.Id, true, 10_000m, 8_000m), (unchangedSku.Id, unchangedSku.IsActive, unchangedSku.RetailPrice, unchangedSku.WholesalePrice));
        Assert.Equal(barcodeOfB, Assert.Single(unchangedSku.Barcodes));
        // Không lời gọi nào ở trên được phát thông điệp, ở cả hai tenant.
        Assert.Equal(eventsOfBBefore, (await factory.SkuEventsAsync(tenantBId)).Count);
        Assert.Equal(eventsOfABefore, (await factory.SkuEventsAsync(tenantAId)).Count);
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
