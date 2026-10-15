namespace Oism.Insights.Domain.Forecasting;

// Tham số dự báo, mặc định theo docs/design/forecast.md mục "Tham số"; đọc từ cấu hình `Forecast` của insights.
public sealed record ForecastSettings
{
    public decimal Alpha { get; init; } = 0.3m;

    public int HistoryDays { get; init; } = 60;

    public int MinDaysWithHistory { get; init; } = 14;

    public int HorizonDays { get; init; } = 14;

    public int LeadTimeDays { get; init; } = 7;

    public int SafetyDays { get; init; } = 3;

    public int BacktestDays { get; init; } = 14;
}
