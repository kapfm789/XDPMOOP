using Oism.Core.Domain.References;

namespace Oism.Core.UnitTests.References;

// Bản sao dựng từ thông điệp trạng thái: chỉ ghi đè khi version lớn hơn bản đang giữ (docs/design/data-model/core.md).
public sealed class ReferenceTests
{
    [Fact]
    [Trait("UseCase", "UC-PROD-02 AC-4")]
    public void SkuRef_Apply_OverwritesOnlyWithAHigherVersion()
    {
        var sku = new SkuRef(Guid.NewGuid());

        Assert.True(sku.Apply("AO-01", "Áo thun", ["2000000000015"], 150_000, 120_000, isActive: true, version: 2));
        Assert.False(sku.Apply("AO-01", "Bản cũ", [], 1, 1, isActive: false, version: 1));
        Assert.False(sku.Apply("AO-01", "Cùng version", [], 1, 1, isActive: false, version: 2));

        Assert.Equal(("AO-01", "Áo thun", 150_000m, 120_000m, true, 2L),
            (sku.SkuCode, sku.Name, sku.RetailPrice, sku.WholesalePrice, sku.IsActive, sku.Version));
        Assert.Equal(["2000000000015"], sku.Barcodes);

        Assert.True(sku.Apply("AO-01", "Áo thun", [], 160_000, 130_000, isActive: false, version: 3));
        Assert.Equal((160_000m, false, 3L), (sku.RetailPrice, sku.IsActive, sku.Version));
        Assert.Empty(sku.Barcodes);
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-04 AC-4")]
    public void BranchRef_Apply_OverwritesOnlyWithAHigherVersion()
    {
        var branch = new BranchRef(Guid.NewGuid());

        Assert.True(branch.Apply("CH-01", "Cửa hàng 1", "Store", isActive: true, version: 1));
        Assert.True(branch.Apply("CH-01", "Kho 1", "Warehouse", isActive: false, version: 3));
        Assert.False(branch.Apply("CH-01", "Bản cũ", "Store", isActive: true, version: 2));

        Assert.Equal(("CH-01", "Kho 1", "Warehouse", false, 3L),
            (branch.Code, branch.Name, branch.Type, branch.IsActive, branch.Version));
    }
}
