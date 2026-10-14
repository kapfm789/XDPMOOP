using Oism.SharedKernel;

namespace Oism.Insights.Domain.Stock;

// Trạng thái tồn mới nhất của một SKU tại một chi nhánh, dựng từ StockChanged
// (docs/design/data-model/insights.md mục "stock_snapshots").
public sealed class StockSnapshot(Guid branchId, Guid skuId) : ITenantOwned
{
    public Guid TenantId { get; private set; }

    public Guid BranchId { get; private set; } = branchId;

    public Guid SkuId { get; private set; } = skuId;

    public int OnHand { get; private set; }

    public int Reserved { get; private set; }

    public int Available { get; private set; }

    public decimal AvgCost { get; private set; }

    public int? Threshold { get; private set; }

    // 0 khi chưa nhận thông điệp nào; version của dòng số dư ở `core` bắt đầu từ 1.
    public long Version { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    // ADR-0008: cảnh báo khi available <= threshold; SKU chưa đặt ngưỡng không bao giờ bị cảnh báo.
    public bool IsLow => Threshold is { } threshold && Available <= threshold;

    // Thông điệp trạng thái có thể tới trùng hoặc sai thứ tự: chỉ ghi đè khi version lớn hơn bản đang giữ.
    // BecameLow khi lần ghi đè này đưa SKU từ trên ngưỡng (hoặc chưa có ngưỡng) xuống bằng hoặc dưới ngưỡng:
    // lúc đó mới đẩy thông báo LowStock (docs/design/events.md mục "Thông báo SignalR").
    public StockSnapshotUpdate Apply(
        int onHand, int reserved, int available, decimal avgCost, int? threshold, long version, DateTimeOffset updatedAt)
    {
        if (version <= Version)
            return StockSnapshotUpdate.Ignored;

        var wasLow = IsLow;
        OnHand = onHand;
        Reserved = reserved;
        Available = available;
        AvgCost = avgCost;
        Threshold = threshold;
        Version = version;
        UpdatedAt = updatedAt;
        return !wasLow && IsLow ? StockSnapshotUpdate.BecameLow : StockSnapshotUpdate.Applied;
    }
}

public enum StockSnapshotUpdate
{
    Ignored,
    Applied,
    BecameLow,
}
