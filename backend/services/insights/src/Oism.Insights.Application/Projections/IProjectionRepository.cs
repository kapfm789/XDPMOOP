using Oism.Insights.Domain.References;
using Oism.Insights.Domain.Sales;
using Oism.Insights.Domain.Stock;

namespace Oism.Insights.Application.Projections;

// Bảng đọc dựng từ event, cài đặt ở Infrastructure. Thay đổi được lưu khi consumer base commit.
public interface IProjectionRepository
{
    // Những dòng đơn trong danh sách đã có trong sales_facts của tenant.
    Task<IReadOnlySet<Guid>> ListRecordedOrderItemsAsync(IReadOnlyCollection<Guid> orderItemIds, CancellationToken ct);

    void Add(SalesFact fact);

    Task<StockSnapshot?> FindStockSnapshotAsync(Guid branchId, Guid skuId, CancellationToken ct);

    void Add(StockSnapshot snapshot);

    Task<SkuName?> FindSkuNameAsync(Guid skuId, CancellationToken ct);

    void Add(SkuName sku);
}
