using Oism.SharedKernel;

namespace Oism.Insights.Domain.References;

// Mã và tên SKU để hiển thị trong báo cáo và cảnh báo, dựng từ SkuUpserted
// (docs/design/data-model/insights.md mục "Các bảng còn lại").
public sealed class SkuName(Guid skuId) : ITenantOwned
{
    public Guid TenantId { get; private set; }

    public Guid SkuId { get; private set; } = skuId;

    public string SkuCode { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    // 0 khi chưa nhận thông điệp nào; version của `catalog` bắt đầu từ 1.
    public long Version { get; private set; }

    // Chỉ ghi đè khi version lớn hơn bản đang giữ. False khi thông điệp bị bỏ qua.
    public bool Apply(string skuCode, string name, long version)
    {
        if (version <= Version)
            return false;

        SkuCode = skuCode;
        Name = name;
        Version = version;
        return true;
    }
}
