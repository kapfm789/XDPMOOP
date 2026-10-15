namespace Oism.Insights.Application.Alerts.ListLowStock;

public sealed record ListLowStockQuery(Guid? BranchId);

public sealed record LowStockAlertRow(Guid BranchId, Guid SkuId, string? SkuCode, string? Name, int Available, int Threshold);

// UC-REP-03 AC-1..3: SKU có ngưỡng và available <= threshold (ADR-0008). Dòng rời danh sách ngay khi StockChanged
// đưa available lên trên ngưỡng; SKU chưa đặt ngưỡng không bao giờ có mặt.
public sealed class ListLowStockHandler(IAlertQueries alerts)
{
    public Task<IReadOnlyList<LowStockAlertRow>> Handle(ListLowStockQuery query, CancellationToken ct) =>
        alerts.ListLowStockAsync(query.BranchId, ct);
}
