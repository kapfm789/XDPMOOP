using Microsoft.EntityFrameworkCore;
using Oism.Insights.Application.Alerts;
using Oism.Insights.Application.Alerts.ListLowStock;

namespace Oism.Insights.Infrastructure.Repositories;

// Chỉ đọc. Mã và tên SKU lấy từ sku_names của chính schema insights.
internal sealed class AlertQueries(InsightsDbContext db) : IAlertQueries
{
    // Điều kiện viết lại từ StockSnapshot.IsLow để database lọc: có ngưỡng và available <= threshold (ADR-0008).
    public async Task<IReadOnlyList<LowStockAlertRow>> ListLowStockAsync(Guid? branchId, CancellationToken ct) =>
        await (
                from snapshot in db.StockSnapshots.AsNoTracking()
                where snapshot.Threshold != null && snapshot.Available <= snapshot.Threshold
                    && (branchId == null || snapshot.BranchId == branchId)
                join sku in db.SkuNames.AsNoTracking() on snapshot.SkuId equals sku.SkuId into skus
                from sku in skus.DefaultIfEmpty()
                orderby snapshot.Available, sku.SkuCode, snapshot.BranchId
                select new LowStockAlertRow(
                    snapshot.BranchId, snapshot.SkuId, sku.SkuCode, sku.Name, snapshot.Available, snapshot.Threshold!.Value))
            .ToListAsync(ct);
}
