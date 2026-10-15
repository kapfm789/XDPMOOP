using Oism.Insights.Domain.Forecasting;
using Oism.Insights.Domain.Sales;

namespace Oism.Insights.Application.Forecasting.RunForecast;

public sealed record RunForecastCommand;

// UC-AI-02 AC-1..3: chạy dự báo cho tenant hiện tại, mỗi chi nhánh và SKU một kết quả, kèm backtest WAPE
// (docs/design/forecast.md). Gọi từ POST /forecast/run và từ job đêm. Không tạo phiếu nhập, không ghi sổ (D-14).
public sealed class RunForecastHandler(
    IUnitOfWork unitOfWork, ISalesHistorySource source, IForecastRepository forecasts, ForecastSettings settings, IClock clock)
{
    public async Task<Guid> Handle(RunForecastCommand command, CancellationToken ct)
    {
        var runId = Guid.NewGuid();
        var ranAt = clock.UtcNow;
        try
        {
            await using var transaction = await unitOfWork.BeginAsync(ct);

            // Lịch sử tính tới hết ngày hôm qua theo giờ Việt Nam.
            var lastDay = ReportCalendar.DateOf(ranAt).AddDays(-1);
            var histories = await source.LoadAsync(lastDay.AddDays(1 - settings.HistoryDays), lastDay, ct);
            var available = await source.LoadAvailableAsync(ct);

            var forecaster = new DemandForecaster(settings);
            forecasts.Add(ForecastRun.Succeeded(runId, ranAt, forecaster.Backtest(histories, lastDay)));
            forecasts.AddRange(histories.Select(history => forecaster.Forecast(
                runId, history, available.GetValueOrDefault((history.BranchId, history.SkuId)), lastDay)));

            await transaction.CommitAsync(ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // docs/design/forecast.md mục "Chạy": lần chạy lỗi vẫn được ghi lại với trạng thái Failed. Transaction trên đã
            // rollback hết; dòng Failed đi trong một transaction mới nên kết quả của lần thành công trước còn nguyên.
            await using var transaction = await unitOfWork.BeginAsync(ct);
            forecasts.Add(ForecastRun.Failed(runId, ranAt, exception.Message));
            await transaction.CommitAsync(ct);
        }

        return runId;
    }
}
