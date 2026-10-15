using Oism.Insights.Application.Reports.GetGrossProfit;
using Oism.Insights.Application.Reports.GetInventoryValue;
using Oism.Insights.Application.Reports.GetSlowMovers;
using Oism.Insights.Application.Reports.GetTopSellers;

namespace Oism.Insights.Application.Reports;

// Truy vấn chỉ đọc của báo cáo, cài đặt ở Infrastructure.
public interface IReportQueries
{
    // Một dòng cho mỗi nhóm theo filter.GroupBy, khớp mọi điều kiện lọc; với GroupBy None là đúng một dòng tổng,
    // hoặc không dòng nào khi không có số liệu. Ngày đã tổng hợp đọc daily_aggregates, ngày còn lại đọc sales_facts.
    // Nhóm theo ngày xếp theo ngày tăng dần; các nhóm khác xếp theo doanh thu giảm dần.
    Task<IReadOnlyList<GrossProfitRow>> SumGrossProfitAsync(GrossProfitFilter filter, CancellationToken ct);

    // Dòng stock_snapshots còn tồn (on_hand > 0). Có giá vốn thì xếp theo giá trị giảm dần; không thì theo on_hand giảm dần.
    Task<InventoryValueReport> ListInventoryValueAsync(GetInventoryValueQuery query, CancellationToken ct);

    // Cộng số lượng của sales_facts từ filter.Since, theo SKU, xếp giảm dần.
    Task<IReadOnlyList<TopSellerRow>> ListTopSellersAsync(SalesWindowFilter filter, CancellationToken ct);

    // SKU còn tồn kèm số lượng bán từ filter.Since (0 khi không bán), xếp tăng dần theo số lượng bán.
    Task<IReadOnlyList<SlowMoverRow>> ListSlowMoversAsync(SalesWindowFilter filter, CancellationToken ct);
}
