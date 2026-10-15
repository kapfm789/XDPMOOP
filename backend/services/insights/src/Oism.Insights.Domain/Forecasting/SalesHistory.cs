namespace Oism.Insights.Domain.Forecasting;

// Lịch sử bán của một SKU tại một chi nhánh, gom theo ngày giờ Việt Nam (docs/design/forecast.md mục "Đầu vào").
// FirstSale là ngày bán đầu tiên tính trên toàn bộ lịch sử, null khi chưa bán lần nào. DailyQuantities chỉ chứa ngày có bán;
// ngày vắng mặt được tính là 0.
public sealed record SalesHistory(Guid BranchId, Guid SkuId, DateOnly? FirstSale, IReadOnlyDictionary<DateOnly, int> DailyQuantities)
{
    // Số ngày có lịch sử tính tới hết ngày lastDay: từ ngày bán đầu tiên tới lastDay, tính cả hai đầu.
    public int DaysOfHistory(DateOnly lastDay) =>
        FirstSale is { } first && first <= lastDay ? lastDay.DayNumber - first.DayNumber + 1 : 0;

    // Chuỗi số lượng bán từng ngày, cũ trước mới sau, từ max(from, FirstSale) tới to. Ngày không bán là 0.
    public IReadOnlyList<int> Series(DateOnly from, DateOnly to)
    {
        if (FirstSale is not { } first)
            return [];

        var start = first > from ? first : from;
        var series = new List<int>();
        for (var day = start; day <= to; day = day.AddDays(1))
            series.Add(DailyQuantities.GetValueOrDefault(day));
        return series;
    }
}
