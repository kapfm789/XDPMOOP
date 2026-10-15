using Oism.Insights.Application.Alerts.ListLowStock;

namespace Oism.Insights.Application.Alerts;

// Truy vấn chỉ đọc của cảnh báo, cài đặt ở Infrastructure.
public interface IAlertQueries
{
    // Dòng stock_snapshots có threshold khác null và available <= threshold, xếp available tăng dần.
    Task<IReadOnlyList<LowStockAlertRow>> ListLowStockAsync(Guid? branchId, CancellationToken ct);
}
