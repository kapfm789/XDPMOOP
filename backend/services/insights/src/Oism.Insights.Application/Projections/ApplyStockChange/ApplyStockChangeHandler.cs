using Oism.Contracts;
using Oism.Insights.Domain.Stock;

namespace Oism.Insights.Application.Projections.ApplyStockChange;

// Ghi đè stock_snapshots theo StockChanged; bản có version nhỏ hơn hoặc bằng bản đang giữ bị bỏ qua.
// Không mở transaction: consumer base đã mở, ghi inbox và sẽ commit.
public sealed class ApplyStockChangeHandler(IProjectionRepository projections, IClock clock)
{
    public async Task Handle(StockChanged message, CancellationToken ct)
    {
        var snapshot = await projections.FindStockSnapshotAsync(message.BranchId, message.SkuId, ct);
        if (snapshot is null)
            projections.Add(snapshot = new StockSnapshot(message.BranchId, message.SkuId));

        snapshot.Apply(
            message.OnHand, message.Reserved, message.Available, message.AvgCost, message.Threshold, message.Version, clock.UtcNow);
    }
}
