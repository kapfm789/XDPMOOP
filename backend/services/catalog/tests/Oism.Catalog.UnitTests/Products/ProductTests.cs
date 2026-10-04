using Oism.Catalog.Domain;

namespace Oism.Catalog.UnitTests.Products;

public sealed class ProductTests
{
    private static readonly IReadOnlyDictionary<string, string> NoAttributes = new Dictionary<string, string>();

    private static Product NewProduct(bool hasVariants) =>
        Product.Create(" Áo thun A ", Guid.NewGuid(), brandId: null, description: " ", hasVariants);

    [Fact]
    [Trait("UseCase", "UC-PROD-02 AC-1")]
    public void AddSku_ProductWithoutVariants_TakesExactlyOne()
    {
        var product = NewProduct(hasVariants: false);

        Assert.True(product.CanAddSku);
        var sku = product.AddSku(" BUT-01 ", NoAttributes, 5_000, 4_000);

        Assert.False(product.CanAddSku);
        Assert.Throws<InvalidOperationException>(() => product.AddSku("BUT-02", NoAttributes, 0, 0));
        Assert.Equal(("BUT-01", product.Id, 5_000m, 4_000m, true, 1L),
            (sku.SkuCode, sku.ProductId, sku.RetailPrice, sku.WholesalePrice, sku.IsActive, sku.Version));
        Assert.Equal("Áo thun A", product.DisplayNameOf(sku));
        Assert.Null(product.Description);
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-02 AC-2")]
    public void DisplayNameOf_SkuWithAttributes_IsProductNameThenValuesInAttributeNameOrder()
    {
        var product = NewProduct(hasVariants: true);

        var sku = product.AddSku("AO-DEN-M", new Dictionary<string, string> { ["size"] = "M", ["color"] = "Đen" }, 0, 0);
        product.AddSku("AO-DEN-L", new Dictionary<string, string> { ["color"] = "Đen", ["size"] = "L" }, 0, 0);

        Assert.True(product.CanAddSku);
        Assert.Equal(2, product.Skus.Count);
        Assert.Equal("Áo thun A, Đen, M", product.DisplayNameOf(sku));
    }

    // SkuUpserted mang version để bên nhận bỏ qua bản cũ: mỗi thay đổi của SKU, mã vạch, giá phải tăng đúng 1.
    [Fact]
    [Trait("UseCase", "UC-PROD-02 AC-4")]
    public void Sku_EveryChange_IncrementsVersionByOne()
    {
        var sku = NewProduct(hasVariants: false).AddSku("BUT-01", NoAttributes, 0, 0);

        sku.SetPrices(5_000, 4_000);
        Assert.Equal((5_000m, 4_000m, 2L), (sku.RetailPrice, sku.WholesalePrice, sku.Version));

        var barcode = sku.AddBarcode("2000000000015", BarcodeSymbology.EAN13);
        Assert.Equal((sku.Id, "2000000000015", BarcodeSymbology.EAN13), (barcode.SkuId, barcode.Code, barcode.Symbology));
        Assert.Equal(3, sku.Version);

        // Xóa mã vạch không có thì không đổi gì.
        Assert.False(sku.RemoveBarcode(Guid.NewGuid()));
        Assert.Equal(3, sku.Version);
        Assert.True(sku.RemoveBarcode(barcode.Id));
        Assert.Empty(sku.Barcodes);
        Assert.Equal(4, sku.Version);

        sku.Update(new Dictionary<string, string> { ["color"] = "Xanh" }, isActive: false);
        Assert.False(sku.IsActive);
        Assert.Equal(5, sku.Version);
    }

    [Fact]
    [Trait("UseCase", "UC-PROD-02 AC-4")]
    [Trait("UseCase", "UC-PROD-02 AC-5")]
    public void Update_NameOrActiveFlagChanges_EverySkuGetsANewVersion()
    {
        var product = NewProduct(hasVariants: true);
        var (categoryId, first, second) = (
            product.CategoryId, product.AddSku("A-01", NoAttributes, 0, 0), product.AddSku("A-02", NoAttributes, 0, 0));

        // Đổi danh mục, thương hiệu, mô tả không đổi trạng thái SKU gửi sang service khác.
        Assert.False(product.Update("Áo thun A", Guid.NewGuid(), Guid.NewGuid(), "Cotton", isActive: true));
        Assert.Equal((1L, 1L), (first.Version, second.Version));

        Assert.True(product.Update("Áo thun B", categoryId, null, null, isActive: true));
        Assert.Equal((2L, 2L), (first.Version, second.Version));
        Assert.True(product.IsSellable(first));

        // Ngừng bán sản phẩm là ngừng bán mọi SKU của nó, kể cả SKU còn bật.
        Assert.True(product.Update("Áo thun B", categoryId, null, null, isActive: false));
        Assert.Equal((3L, 3L), (first.Version, second.Version));
        Assert.True(first.IsActive);
        Assert.False(product.IsSellable(first));
    }
}
