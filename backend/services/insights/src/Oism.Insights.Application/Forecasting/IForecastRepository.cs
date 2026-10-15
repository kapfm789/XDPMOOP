using Oism.Insights.Domain.Forecasting;

namespace Oism.Insights.Application.Forecasting;

// Đầu vào của dự báo, đọc từ sales_facts và stock_snapshots của tenant hiện tại. Cài đặt ở Infrastructure.
public interface ISalesHistorySource
{
    // Một phần tử cho mỗi chi nhánh và SKU từng bán tới hết ngày `to`, hoặc đang có dòng stock_snapshots.
    // FirstSale tính trên toàn bộ lịch sử tới hết `to`; DailyQuantities chỉ gồm các ngày từ `from` tới `to` (giờ Việt Nam).
    Task<IReadOnlyList<SalesHistory>> LoadAsync(DateOnly from, DateOnly to, CancellationToken ct);

    // Tồn khả dụng hiện tại theo chi nhánh và SKU.
    Task<IReadOnlyDictionary<(Guid BranchId, Guid SkuId), int>> LoadAvailableAsync(CancellationToken ct);
}

// Ghi lần chạy và kết quả; được lưu khi transaction của handler commit.
public interface IForecastRepository
{
    void Add(ForecastRun run);

    void AddRange(IEnumerable<ForecastResult> results);
}
