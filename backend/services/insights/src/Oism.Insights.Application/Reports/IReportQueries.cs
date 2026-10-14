using Oism.Insights.Application.Reports.GetGrossProfit;

namespace Oism.Insights.Application.Reports;

// Truy vấn chỉ đọc của báo cáo, cài đặt ở Infrastructure.
public interface IReportQueries
{
    // Một dòng cho mỗi nhóm theo filter.GroupBy, khớp mọi điều kiện lọc; với GroupBy None là đúng một dòng tổng,
    // hoặc không dòng nào khi không có số liệu. Ngày đã tổng hợp đọc daily_aggregates, ngày còn lại đọc sales_facts.
    // Nhóm theo ngày xếp theo ngày tăng dần; các nhóm khác xếp theo doanh thu giảm dần.
    Task<IReadOnlyList<GrossProfitRow>> SumGrossProfitAsync(GrossProfitFilter filter, CancellationToken ct);
}
