using Oism.Contracts;
using Oism.Insights.Application.Notifications;
using Oism.Insights.Domain.Stock;

namespace Oism.Insights.Application.Projections.ApplyStockChange;

// Ghi đè stock_snapshots theo StockChanged; bản có version nhỏ hơn hoặc bằng bản đang giữ bị bỏ qua.
// SKU vừa chạm ngưỡng thì đẩy LowStock (UC-REP-03 AC-1). Không mở transaction: consumer base đã mở, ghi inbox và sẽ commit.
public sealed class ApplyStockChangeHandler(IProjectionRepository projections, INotificationPublisher notifications, IClock clock)
{
    public async Task Handle(StockChanged message, CancellationToken ct)
    {
        var snapshot = await projections.FindStockSnapshotAsync(message.BranchId, message.SkuId, ct);
        if (snapshot is null)
            projections.Add(snapshot = new StockSnapshot(message.BranchId, message.SkuId));

        var update = snapshot.Apply(
            message.OnHand, message.Reserved, message.Available, message.AvgCost, message.Threshold, message.Version, clock.UtcNow);
        if (update != StockSnapshotUpdate.BecameLow)
            return;

        // SKU chưa tới qua SkuUpserted thì thông báo vẫn đi, chỉ thiếu mã và tên.
        var sku = await projections.FindSkuNameAsync(message.SkuId, ct);
        notifications.LowStock(new LowStockNotification(
            snapshot.BranchId, snapshot.SkuId, sku?.SkuCode, sku?.Name, snapshot.Available, snapshot.Threshold!.Value));
    }
}
