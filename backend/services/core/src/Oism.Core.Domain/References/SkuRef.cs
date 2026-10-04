using Oism.SharedKernel;

namespace Oism.Core.Domain.References;

// Bản sao SKU của `catalog`, dựng từ SkuUpserted. Chỉ consumer ghi vào bảng này (docs/design/data-model/core.md).
public sealed class SkuRef(Guid skuId) : ITenantOwned
{
    public Guid TenantId { get; private set; }

    public Guid SkuId { get; private set; } = skuId;

    public string SkuCode { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string[] Barcodes { get; private set; } = [];

    public decimal RetailPrice { get; private set; }

    public decimal WholesalePrice { get; private set; }

    public bool IsActive { get; private set; }

    // 0 khi chưa nhận thông điệp nào; version của `catalog` bắt đầu từ 1.
    public long Version { get; private set; }

    // Thông điệp trạng thái có thể tới trùng hoặc sai thứ tự: chỉ ghi đè khi version lớn hơn bản đang giữ.
    // False khi thông điệp bị bỏ qua.
    public bool Apply(
        string skuCode, string name, IEnumerable<string> barcodes, decimal retailPrice, decimal wholesalePrice, bool isActive, long version)
    {
        if (version <= Version)
            return false;

        SkuCode = skuCode;
        Name = name;
        Barcodes = barcodes.ToArray();
        RetailPrice = retailPrice;
        WholesalePrice = wholesalePrice;
        IsActive = isActive;
        Version = version;
        return true;
    }
}
