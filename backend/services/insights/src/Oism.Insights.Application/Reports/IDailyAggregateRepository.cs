using Oism.Insights.Domain.Sales;

namespace Oism.Insights.Application.Reports;

// Tổng bán của một ngày theo chi nhánh, kênh và SKU, cộng từ sales_facts.
public sealed record DailySalesTotal(Guid BranchId, string Channel, Guid SkuId, int Quantity, decimal Revenue, decimal Cogs);

// daily_aggregates của tenant hiện tại, cài đặt ở Infrastructure.
public interface IDailyAggregateRepository
{
    // Cộng sales_facts có ngày giờ Việt Nam bằng date.
    Task<IReadOnlyList<DailySalesTotal>> SumSalesAsync(DateOnly date, CancellationToken ct);

    // Xóa ngay mọi dòng tổng hợp của ngày đó, trong transaction đang mở.
    Task RemoveAsync(DateOnly date, CancellationToken ct);

    void Add(DailyAggregate aggregate);
}
