using Oism.Insights.Application.Forecasting.GetReorderSuggestions;

namespace Oism.Insights.Application.Forecasting;

// Truy vấn chỉ đọc của đề xuất nhập, cài đặt ở Infrastructure.
public interface IForecastQueries
{
    // Lần chạy mới nhất của tenant theo ran_at; succeededOnly chỉ xét lần chạy thành công.
    Task<ForecastRunDto?> FindLatestRunAsync(bool succeededOnly, CancellationToken ct);

    // Kết quả của một lần chạy theo bộ lọc: dòng đủ dữ liệu trước, đề xuất nhập nhiều hơn đứng trước.
    Task<(IReadOnlyList<ReorderSuggestionRow> Items, int Total)> ListResultsAsync(
        Guid runId, GetReorderSuggestionsQuery query, CancellationToken ct);
}
