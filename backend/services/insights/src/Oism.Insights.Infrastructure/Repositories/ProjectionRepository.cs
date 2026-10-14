using Microsoft.EntityFrameworkCore;
using Oism.Insights.Application.Projections;
using Oism.Insights.Domain.References;
using Oism.Insights.Domain.Sales;
using Oism.Insights.Domain.Stock;

namespace Oism.Insights.Infrastructure.Repositories;

internal sealed class ProjectionRepository(InsightsDbContext db) : IProjectionRepository
{
    public async Task<IReadOnlySet<Guid>> ListRecordedOrderItemsAsync(IReadOnlyCollection<Guid> orderItemIds, CancellationToken ct) =>
        (await db.SalesFacts
            .Where(fact => orderItemIds.Contains(fact.OrderItemId))
            .Select(fact => fact.OrderItemId)
            .ToListAsync(ct))
        .ToHashSet();

    public void Add(SalesFact fact) => db.Add(fact);

    public Task<StockSnapshot?> FindStockSnapshotAsync(Guid branchId, Guid skuId, CancellationToken ct) =>
        db.StockSnapshots.SingleOrDefaultAsync(snapshot => snapshot.BranchId == branchId && snapshot.SkuId == skuId, ct);

    public void Add(StockSnapshot snapshot) => db.Add(snapshot);

    public Task<SkuName?> FindSkuNameAsync(Guid skuId, CancellationToken ct) =>
        db.SkuNames.SingleOrDefaultAsync(sku => sku.SkuId == skuId, ct);

    public void Add(SkuName sku) => db.Add(sku);
}
