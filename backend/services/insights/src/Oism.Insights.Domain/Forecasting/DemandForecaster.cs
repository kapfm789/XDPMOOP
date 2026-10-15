namespace Oism.Insights.Domain.Forecasting;

// Dự báo nhu cầu và đề xuất nhập cho từng chi nhánh và SKU, kèm backtest (docs/design/forecast.md, ADR-0010).
// Kết quả chỉ là khuyến nghị: lớp này không tạo phiếu nhập, không ghi sổ, không đổi ngưỡng tồn.
public sealed class DemandForecaster(ForecastSettings settings)
{
    public const string Method = "SimpleExponentialSmoothing";

    // Phương án cơ sở của backtest: trung bình 7 ngày cuối của phần huấn luyện.
    private const int BaselineDays = 7;

    // lastDay là ngày cuối của lịch sử: hôm qua theo giờ Việt Nam. Available là tồn khả dụng hiện tại.
    public ForecastResult Forecast(Guid runId, SalesHistory history, int available, DateOnly lastDay)
    {
        // Bước 1: ít hơn MinDaysWithHistory ngày kể từ ngày bán đầu tiên thì chưa đủ dữ liệu.
        if (history.DaysOfHistory(lastDay) < settings.MinDaysWithHistory)
            return ForecastResult.NotEnoughData(runId, history.BranchId, history.SkuId);

        // Bước 2 đến 4: mức cuối là nhu cầu mỗi ngày; nhu cầu trong tầm nhìn; số lượng đề xuất nhập.
        var dailyLevel = Math.Round(ExponentialSmoothing.Level(history.Series(WindowStart(lastDay), lastDay), settings.Alpha), 4);
        var forecastQty = dailyLevel * settings.HorizonDays;
        var cover = (int)Math.Ceiling(dailyLevel * (settings.LeadTimeDays + settings.SafetyDays));
        return ForecastResult.Of(runId, history.BranchId, history.SkuId, dailyLevel, forecastQty, Math.Max(0, cover - available));
    }

    // Backtest gộp mọi chi nhánh và SKU đủ dữ liệu tính tới mốc cắt (docs/design/forecast.md mục "Backtest").
    // Tổng thực tế của phần kiểm tra bằng 0 thì không có dữ liệu để đánh giá: hai WAPE để trống.
    public BacktestResult Backtest(IEnumerable<SalesHistory> histories, DateOnly lastDay)
    {
        var cutoff = lastDay.AddDays(-settings.BacktestDays);
        var windowStart = WindowStart(lastDay);
        decimal actual = 0, forecastError = 0, baselineError = 0;

        foreach (var history in histories)
        {
            // Đủ dữ liệu xét tại mốc cắt, để việc chọn SKU cũng không dùng ngày sau mốc cắt.
            if (history.DaysOfHistory(cutoff) < settings.MinDaysWithHistory)
                continue;

            var prediction = PredictAt(history, windowStart, cutoff);
            foreach (var quantity in history.Series(cutoff.AddDays(1), lastDay))
            {
                actual += quantity;
                forecastError += Math.Abs(quantity - prediction.Forecast);
                baselineError += Math.Abs(quantity - prediction.Baseline);
            }
        }

        return actual == 0
            ? new BacktestResult(null, null)
            : new BacktestResult(Math.Round(forecastError / actual, 4), Math.Round(baselineError / actual, 4));
    }

    // Dự báo dùng cho mọi ngày sau mốc cắt, dựng chỉ từ các ngày từ windowStart tới hết ngày cutoff (T18).
    public BacktestPrediction PredictAt(SalesHistory history, DateOnly windowStart, DateOnly cutoff)
    {
        var training = history.Series(windowStart, cutoff);
        var lastWeek = training.Skip(Math.Max(0, training.Count - BaselineDays)).ToList();
        var baseline = lastWeek.Count == 0 ? 0 : (decimal)lastWeek.Sum() / lastWeek.Count;
        return new BacktestPrediction(ExponentialSmoothing.Level(training, settings.Alpha), baseline);
    }

    // Cửa sổ HistoryDays ngày tính tới hết lastDay.
    private DateOnly WindowStart(DateOnly lastDay) => lastDay.AddDays(1 - settings.HistoryDays);
}

public sealed record BacktestPrediction(decimal Forecast, decimal Baseline);

public sealed record BacktestResult(decimal? ForecastWape, decimal? BaselineWape);
