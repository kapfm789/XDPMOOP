using Oism.SharedKernel;

namespace Oism.Insights.Domain.Forecasting;

public enum ForecastRunStatus
{
    Succeeded,
    Failed,
}

// Một lần chạy dự báo của một tenant (docs/design/data-model/insights.md mục "Các bảng còn lại").
// Lần chạy lỗi chỉ ghi trạng thái và thông báo lỗi; kết quả của lần thành công trước vẫn được giữ.
public sealed class ForecastRun : ITenantOwned
{
    private const int MaxErrorLength = 2000;

    private ForecastRun()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public DateTimeOffset RanAt { get; private set; }

    public string Method { get; private set; } = null!;

    public ForecastRunStatus Status { get; private set; }

    public string? Error { get; private set; }

    public decimal? BaselineWape { get; private set; }

    public decimal? ForecastWape { get; private set; }

    public static ForecastRun Succeeded(Guid id, DateTimeOffset ranAt, BacktestResult backtest) => new()
    {
        Id = id,
        RanAt = ranAt,
        Method = DemandForecaster.Method,
        Status = ForecastRunStatus.Succeeded,
        BaselineWape = backtest.BaselineWape,
        ForecastWape = backtest.ForecastWape,
    };

    public static ForecastRun Failed(Guid id, DateTimeOffset ranAt, string error) => new()
    {
        Id = id,
        RanAt = ranAt,
        Method = DemandForecaster.Method,
        Status = ForecastRunStatus.Failed,
        Error = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error,
    };
}
