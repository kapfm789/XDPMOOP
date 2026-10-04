using System.Net;
using System.Net.Http.Json;
using Oism.BuildingBlocks.Auth;
using Oism.Catalog.Application.Products;
using static Oism.Catalog.IntegrationTests.ApiExtensions;

namespace Oism.Catalog.IntegrationTests.Products;

// UC-PROD-02: sản phẩm và SKU.
public sealed class ProductTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private HttpClient Owner => factory.CreateClient(Roles.Owner, _tenantId);

    private HttpClient Staff => factory.CreateClient(Roles.Staff, _tenantId);

    [Fact]
    [Trait("UseCase", "UC-PROD-02 AC-1")]
    public async Task CreateProduct_WithoutVariants_HasExactlyOneSkuNamedAfterTheProduct()
    {
        var brand = await Staff.CreateBrandAsync("Thiên Long");
        var category = await Staff.CreateCategoryAsync("Văn phòng phẩm");

        var response = await Staff.PostAsJsonAsync("/products", new
        {
            name = " Bút bi ", categoryId = category.Id, brandId = brand.Id, description = " Mực xanh ", hasVariants = false,
            skus = new[] { Sku(" BUT-01 ") },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var product = (await response.Content.ReadFromJsonAsync<ProductDto>())!;
        Assert.Equal(("Bút bi", category.Id, brand.Id, "Mực xanh", false, true),
            (product.Name, product.CategoryId, product.BrandId, product.Description, product.HasVariants, product.IsActive));
        var sku = Assert.Single(product.Skus);
        Assert.Equal(("BUT-01", "Bút bi", product.Id, true), (sku.SkuCode, sku.Name, sku.ProductId, sku.IsActive));
        Assert.Empty(sku.Attributes);
        Assert.Empty(sku.Barcodes);
        Assert.Equal(sku.Id, Assert.Single((await Staff.GetProductAsync(product.Id)).Skus).Id);
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-02 AC-1")]
    public async Task CreateProduct_WithoutVariantsButNotExactlyOneSku_Returns400AndSavesNothing()
    {
        var category = await Staff.CreateCategoryAsync("Văn phòng phẩm");

        var none = await Staff.PostProductAsync("Không SKU", category.Id, hasVariants: false);
        var two = await Staff.PostProductAsync("Hai SKU", category.Id, hasVariants: false, Sku("A-01"), Sku("A-02"));
        var noneWithVariants = await Staff.PostProductAsync("Biến thể rỗng", category.Id, hasVariants: true);
        var blankName = await Staff.PostProductAsync(" ", category.Id, hasVariants: false, Sku("A-03"));
        var blankSkuCode = await Staff.PostProductAsync("Mã trống", category.Id, hasVariants: false, Sku(" "));

        await none.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await two.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await noneWithVariants.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await blankName.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await blankSkuCode.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal(0, (await Staff.ListProductsAsync()).Total);
        Assert.Empty(await factory.SkuEventsAsync(_tenantId));
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-02 AC-2")]
    public async Task CreateProduct_WithVariants_EachCombinationIsASkuWithItsOwnCodeAndDisplayName()
    {
        var product = await Staff.CreateProductAsync(
            "Áo thun A", hasVariants: true,
            Sku("AO-DEN-M", new { size = "M", color = "Đen" }),
            Sku("AO-TRANG-L", new { size = "L", color = "Trắng" }));

        var added = await Staff.PostAsJsonAsync($"/products/{product.Id}/skus", Sku("AO-DEN-L", new { color = " Đen ", size = "L" }));

        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        Assert.Equal("Áo thun A, Đen, L", (await added.Content.ReadFromJsonAsync<SkuDto>())!.Name);
        var skus = (await Staff.GetProductAsync(product.Id)).Skus;
        // Thuộc tính xếp theo tên (color trước size), không theo thứ tự gửi lên.
        Assert.Equal(
            [("AO-DEN-L", "Áo thun A, Đen, L"), ("AO-DEN-M", "Áo thun A, Đen, M"), ("AO-TRANG-L", "Áo thun A, Trắng, L")],
            skus.Select(sku => (sku.SkuCode, sku.Name)));
        Assert.Equal("Đen", skus[1].Attributes["color"]);
        Assert.Equal("M", skus[1].Attributes["size"]);
        Assert.Equal(3, skus.Select(sku => sku.Id).Distinct().Count());
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-02 AC-1")]
    public async Task AddSku_ToProductWithoutVariants_Returns400()
    {
        var product = await Staff.CreateProductAsync("Bút bi", hasVariants: false, Sku("BUT-01"));

        var response = await Staff.PostAsJsonAsync($"/products/{product.Id}/skus", Sku("BUT-02"));

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        Assert.Single((await Staff.GetProductAsync(product.Id)).Skus);
    }

    // Điều kiện xong của W1-07: SKU trùng trong tenant bị từ chối.
    [Fact]
    [Trait("UseCase", "UC-PROD-02 AC-3")]
    public async Task Sku_CodeAlreadyInTenant_Returns409ButAnotherTenantMayUseIt()
    {
        var product = await Staff.CreateProductAsync("Áo thun", hasVariants: true, Sku("AO-01"));
        var category = await Staff.CreateCategoryAsync("Khác");
        var otherTenant = factory.CreateClient(Roles.Staff, Guid.NewGuid());

        var duplicateProduct = await Staff.PostProductAsync("Áo khoác", category.Id, hasVariants: false, Sku("AO-01"));
        var duplicateInRequest = await Staff.PostProductAsync("Áo len", category.Id, hasVariants: true, Sku("AO-02"), Sku("AO-02"));
        var duplicateSku = await Staff.PostAsJsonAsync($"/products/{product.Id}/skus", Sku("AO-01"));
        var sameCodeElsewhere = await otherTenant.CreateSkuAsync("AO-01");

        await duplicateProduct.AssertProblemAsync(HttpStatusCode.Conflict, "duplicate");
        await duplicateInRequest.AssertProblemAsync(HttpStatusCode.Conflict, "duplicate");
        await duplicateSku.AssertProblemAsync(HttpStatusCode.Conflict, "duplicate");
        Assert.Equal("AO-01", sameCodeElsewhere.SkuCode);
        Assert.Equal(["Áo thun"], (await Staff.ListProductsAsync()).Items.Select(item => item.Name));
        // Lần tạo bị từ chối không để lại thông điệp nào.
        Assert.Equal(["AO-01"], (await factory.SkuEventsAsync(_tenantId)).Select(message => message.SkuCode));
    }

    // Phía phát của SkuUpserted. Phía nhận kiểm ở Oism.Core.IntegrationTests/Messaging/ReferenceConsumerTests.
    [Fact]
    [Trait("UseCase", "UC-PROD-02 AC-4")]
    [Trait("UseCase", "UC-PROD-02 AC-5")]
    [Trait("UseCase", "UC-PROD-04 AC-5")]
    public async Task Sku_EveryChange_EnqueuesSkuUpsertedWithFullStateAndNextVersion()
    {
        var product = await Owner.CreateProductAsync(
            "Áo thun", hasVariants: true, Sku("AO-01", new { color = "Đen" }, retailPrice: 150_000, wholesalePrice: 120_000));
        var sku = Assert.Single(product.Skus);
        var body = new { name = "Áo phông", categoryId = product.CategoryId, description = (string?)null, isActive = true };

        await Owner.PutAsJsonAsync($"/skus/{sku.Id}", new { attributes = new { color = "Trắng" }, isActive = true });
        await Owner.PutAsJsonAsync($"/skus/{sku.Id}/prices", new { retailPrice = 160_000, wholesalePrice = 130_000 });
        var barcode = await Owner.AddBarcodeAsync(sku.Id, "Code128", "ABC123");
        await Owner.DeleteAsync($"/skus/{sku.Id}/barcodes/{barcode.Id}");
        await Owner.PutAsJsonAsync($"/products/{product.Id}", body);
        // Đổi mô tả không đổi trạng thái của SKU ở service khác; yêu cầu sai không được để lại thông điệp.
        await Owner.PutAsJsonAsync($"/products/{product.Id}", body with { description = "Cotton" });
        await Owner.PutAsJsonAsync($"/skus/{sku.Id}/prices", new { retailPrice = -1, wholesalePrice = 0 });
        // Ngừng bán sản phẩm là ngừng bán SKU của nó.
        await Owner.PutAsJsonAsync($"/products/{product.Id}", body with { isActive = false });

        var events = await factory.SkuEventsAsync(_tenantId);
        Assert.Equal(
            [
                (1, "Áo thun, Đen", "", 150_000m, 120_000m, true),
                (2, "Áo thun, Trắng", "", 150_000m, 120_000m, true),
                (3, "Áo thun, Trắng", "", 160_000m, 130_000m, true),
                (4, "Áo thun, Trắng", "ABC123", 160_000m, 130_000m, true),
                (5, "Áo thun, Trắng", "", 160_000m, 130_000m, true),
                (6, "Áo phông, Trắng", "", 160_000m, 130_000m, true),
                (7, "Áo phông, Trắng", "", 160_000m, 130_000m, false),
            ],
            events.Select(message => (
                message.Version, message.Name, string.Join(",", message.Barcodes), message.RetailPrice, message.WholesalePrice,
                message.IsActive)));
        Assert.All(events, message => Assert.Equal((sku.Id, product.Id, "AO-01"), (message.SkuId, message.ProductId, message.SkuCode)));
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-02 AC-5")]
    public async Task UpdateSku_Deactivate_KeepsTheSkuButMarksItInactive()
    {
        var sku = await Staff.CreateSkuAsync("BUT-01");

        var response = await Staff.PutAsJsonAsync($"/skus/{sku.Id}", new { isActive = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await response.Content.ReadFromJsonAsync<SkuDto>())!.IsActive);
        Assert.False(Assert.Single((await Staff.GetProductAsync(sku.ProductId)).Skus).IsActive);
        Assert.False((await factory.SkuEventsAsync(_tenantId))[^1].IsActive);
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-02 AC-1")]
    public async Task UpdateProduct_ChangesFieldsAndKeepsSkus()
    {
        var product = await Staff.CreateProductAsync("Bút bi", hasVariants: false, Sku("BUT-01"));
        var category = await Staff.CreateCategoryAsync("Danh mục mới");
        var brand = await Staff.CreateBrandAsync("Thiên Long");

        var response = await Staff.PutAsJsonAsync($"/products/{product.Id}", new
        {
            name = " Bút gel ", categoryId = category.Id, brandId = brand.Id, description = "Mực đen", isActive = true,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await Staff.GetProductAsync(product.Id);
        Assert.Equal(("Bút gel", category.Id, brand.Id, "Mực đen", true),
            (updated.Name, updated.CategoryId, updated.BrandId, updated.Description, updated.IsActive));
        Assert.Equal(("BUT-01", "Bút gel"), (updated.Skus[0].SkuCode, updated.Skus[0].Name));
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-02 AC-1")]
    public async Task Product_UnknownIdCategoryOrBrand_Returns404()
    {
        var product = await Staff.CreateProductAsync("Bút bi", hasVariants: false, Sku("BUT-01"));
        var unknown = Guid.NewGuid();

        var unknownCategory = await Staff.PostProductAsync("Vở", unknown, hasVariants: false, Sku("VO-01"));
        var unknownBrand = await Staff.PostAsJsonAsync("/products", new
        {
            name = "Vở", categoryId = product.CategoryId, brandId = unknown, hasVariants = false, skus = new[] { Sku("VO-01") },
        });
        var get = await Staff.GetAsync($"/products/{unknown}");
        var update = await Staff.PutAsJsonAsync($"/products/{unknown}", new { name = "Vở", categoryId = product.CategoryId, isActive = true });
        var moveToUnknownCategory = await Staff.PutAsJsonAsync(
            $"/products/{product.Id}", new { name = "Bút bi", categoryId = unknown, isActive = true });
        var addSku = await Staff.PostAsJsonAsync($"/products/{unknown}/skus", Sku("VO-02"));
        var updateSku = await Staff.PutAsJsonAsync($"/skus/{unknown}", new { isActive = false });

        await unknownCategory.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await unknownBrand.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await get.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await update.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await moveToUnknownCategory.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await addSku.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await updateSku.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        Assert.Equal(1, (await Staff.ListProductsAsync()).Total);
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-02 AC-1")]
    public async Task ListProducts_FiltersByQueryCategoryAndBrand_ReturnsPagesWithSkuCount()
    {
        var brand = await Staff.CreateBrandAsync("Coolmate");
        var shirts = await Staff.CreateCategoryAsync("Áo");
        var pants = await Staff.CreateCategoryAsync("Quần");
        await Staff.PostAsJsonAsync("/products", new
        {
            name = "Ao thun", categoryId = shirts.Id, brandId = brand.Id, hasVariants = true,
            skus = new[] { Sku("AT-M", new { size = "M" }), Sku("AT-L", new { size = "L" }) },
        });
        await Staff.PostProductAsync("Ao khoac", shirts.Id, hasVariants: false, Sku("AK-01"));
        await Staff.PostProductAsync("Quan dai", pants.Id, hasVariants: false, Sku("QD-01"));

        var all = await Staff.ListProductsAsync();
        var byName = await Staff.ListProductsAsync("?query=AO");
        var bySkuCode = await Staff.ListProductsAsync("?query=qd-");
        var byCategory = await Staff.ListProductsAsync($"?categoryId={pants.Id}");
        var byBrand = await Staff.ListProductsAsync($"?brandId={brand.Id}");
        var secondPage = await Staff.ListProductsAsync("?page=2&pageSize=2");
        var invalid = await Staff.GetAsync("/products?page=0&pageSize=500");

        // Xếp theo tên.
        Assert.Equal([("Ao khoac", 1), ("Ao thun", 2), ("Quan dai", 1)], all.Items.Select(item => (item.Name, item.SkuCount)));
        Assert.Equal((1, 20, 3), (all.Page, all.PageSize, all.Total));
        Assert.Equal(["Ao khoac", "Ao thun"], byName.Items.Select(item => item.Name));
        Assert.Equal(["Quan dai"], bySkuCode.Items.Select(item => item.Name));
        Assert.Equal(["Quan dai"], byCategory.Items.Select(item => item.Name));
        Assert.Equal(["Ao thun"], byBrand.Items.Select(item => item.Name));
        Assert.Equal(["Quan dai"], secondPage.Items.Select(item => item.Name));
        Assert.Equal((2, 2, 3), (secondPage.Page, secondPage.PageSize, secondPage.Total));
        await invalid.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
    }

    // Vế "còn sản phẩm" của tiêu chí xóa danh mục.
    [Fact]
    [Trait("UseCase", "UC-PROD-01 AC-3")]
    public async Task DeleteCategory_HasProduct_Returns409AndKeepsIt()
    {
        var product = await Staff.CreateProductAsync("Bút bi", hasVariants: false, Sku("BUT-01"));

        var response = await Staff.DeleteAsync($"/categories/{product.CategoryId}");

        await response.AssertProblemAsync(HttpStatusCode.Conflict, "category_in_use");
        Assert.Equal(product.CategoryId, Assert.Single(await Staff.GetCategoryTreeAsync()).Id);
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-03 AC-3")]
    public async Task Products_Cashier_Returns403()
    {
        var sku = await Staff.CreateSkuAsync("BUT-01");
        var cashier = factory.CreateClient(Roles.Cashier, _tenantId);

        var list = await cashier.GetAsync("/products");
        var get = await cashier.GetAsync($"/products/{sku.ProductId}");
        var create = await cashier.PostProductAsync("Bị chặn", Guid.NewGuid(), hasVariants: false, Sku("X-01"));
        var addSku = await cashier.PostAsJsonAsync($"/products/{sku.ProductId}/skus", Sku("X-02"));
        var updateSku = await cashier.PutAsJsonAsync($"/skus/{sku.Id}", new { isActive = false });
        var addBarcode = await cashier.PostBarcodeAsync(sku.Id, "EAN13");

        foreach (var response in new[] { list, get, create, addSku, updateSku, addBarcode })
            await response.AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
    }
}
