using Oism.Insights.Domain.Forecasting;

namespace Oism.Insights.UnitTests.Forecasting;

// docs/design/forecast.md: san bằng hàm mũ đơn, đề xuất nhập và backtest WAPE.
public sealed class DemandForecasterTests
{
    private static readonly DateOnly LastDay = new(2026, 10, 20);
    private static readonly ForecastSettings Settings = new();
    private static readonly DemandForecaster Forecaster = new(Settings);
    private static readonly Guid RunId = Guid.NewGuid();

    [Fact]
    [Trait("UseCase", "UC-AI-02 AC-1")]
    public void Level_Series_SmoothedWithAlpha()
    {
        Assert.Equal(0m, ExponentialSmoothing.Level([], 0.3m));
        Assert.Equal(10m, ExponentialSmoothing.Level([10], 0.3m));
        // L2 = 0,3 × 20 + 0,7 × 10 = 13; L3 = 0,3 × 30 + 0,7 × 13 = 18,1.
        Assert.Equal(13m, ExponentialSmoothing.Level([10, 20], 0.3m));
        Assert.Equal(18.1m, ExponentialSmoothing.Level([10, 20, 30], 0.3m));
    }

    // Nhu cầu đều 4 mỗi ngày: dự báo 14 ngày là 56; đề xuất nhập là làm tròn lên 4 × (7 + 3) trừ tồn khả dụng, không âm.
    [Theory]
    [Trait("UseCase", "UC-AI-01 AC-1")]
    [InlineData(15, 25)]
    [InlineData(40, 0)]
    [InlineData(55, 0)]
    public void Forecast_SteadyDemand_ForecastAndSuggestedReorder(int available, int expectedReorder)
    {
        var history = Daily(days: 30, _ => 4);

        var result = Forecaster.Forecast(RunId, history, available, LastDay);

        Assert.True(result.HasEnoughData);
        Assert.Equal((4m, 56m, expectedReorder), (result.DailyLevel, result.ForecastQty, result.SuggestedReorderQty));
    }

    [Fact]
    [Trait("UseCase", "UC-AI-01 AC-1")]
    public void Forecast_FractionalLevel_ReorderRoundsUp()
    {
        // Bán 1 cái ngày đầu rồi thôi: mức cuối là một số lẻ rất nhỏ, nhưng lượng cần cho 10 ngày vẫn làm tròn lên thành 1.
        var history = new SalesHistory(Guid.NewGuid(), Guid.NewGuid(), LastDay.AddDays(-20), Quantities(LastDay.AddDays(-20), 21, day => day == 0 ? 1 : 0));
        var level = ExponentialSmoothing.Level(history.Series(LastDay.AddDays(-59), LastDay), 0.3m);

        var result = Forecaster.Forecast(RunId, history, available: 0, LastDay);

        Assert.Equal(Math.Round(level, 4), result.DailyLevel);
        Assert.Equal((int)Math.Ceiling(Math.Round(level, 4) * 10), result.SuggestedReorderQty);
    }

    // T19: SKU mới có 5 ngày lịch sử, hay chưa bán lần nào, là chưa đủ dữ liệu; ba trường số để trống.
    [Theory]
    [Trait("Scenario", "T19")]
    [Trait("UseCase", "UC-AI-01 AC-2")]
    [InlineData(5)]
    [InlineData(13)]
    [InlineData(0)]
    public void Forecast_LessThan14DaysOfHistory_NotEnoughData(int days)
    {
        var history = days == 0
            ? new SalesHistory(Guid.NewGuid(), Guid.NewGuid(), null, new Dictionary<DateOnly, int>())
            : Daily(days, _ => 9);

        var result = Forecaster.Forecast(RunId, history, available: 0, LastDay);

        Assert.False(result.HasEnoughData);
        Assert.Equal(((decimal?)null, (decimal?)null, (int?)null), (result.DailyLevel, result.ForecastQty, result.SuggestedReorderQty));
    }

    [Fact]
    [Trait("UseCase", "UC-AI-01 AC-2")]
    public void Forecast_Exactly14DaysOfHistory_EnoughData() =>
        Assert.True(Forecaster.Forecast(RunId, Daily(14, _ => 1), available: 0, LastDay).HasEnoughData);

    // Ngày không bán tính là 0; chuỗi bắt đầu từ ngày bán đầu tiên, không đệm số 0 trước khi SKU có mặt.
    [Fact]
    [Trait("UseCase", "UC-AI-02 AC-1")]
    public void Series_FromFirstSaleOrWindowStart_MissingDaysAreZero()
    {
        var history = new SalesHistory(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 15),
            new Dictionary<DateOnly, int> { [new(2026, 10, 15)] = 3, [new(2026, 10, 18)] = 2 });

        Assert.Equal([3, 0, 0, 2, 0, 0], history.Series(new DateOnly(2026, 8, 1), LastDay));
        Assert.Equal([0, 2, 0, 0], history.Series(new DateOnly(2026, 10, 17), LastDay));
        Assert.Equal(6, history.DaysOfHistory(LastDay));
        Assert.Equal(0, history.DaysOfHistory(new DateOnly(2026, 10, 14)));
    }

    // Lịch sử cũ hơn cửa sổ 60 ngày không vào chuỗi.
    [Fact]
    [Trait("UseCase", "UC-AI-02 AC-1")]
    public void Forecast_SalesOlderThanWindow_Ignored()
    {
        var history = new SalesHistory(
            Guid.NewGuid(), Guid.NewGuid(), LastDay.AddDays(-100),
            new Dictionary<DateOnly, int> { [LastDay.AddDays(-100)] = 500, [LastDay.AddDays(-10)] = 0 });

        var result = Forecaster.Forecast(RunId, history, available: 0, LastDay);

        Assert.Equal((true, 0m, 0), (result.HasEnoughData, result.DailyLevel, result.SuggestedReorderQty));
    }

    // T18: dự báo cho phần kiểm tra chỉ dựng từ các ngày tới hết mốc cắt. Hai lịch sử giống nhau tới mốc cắt cho cùng dự báo,
    // dù sau mốc cắt khác hẳn nhau.
    [Fact]
    [Trait("Scenario", "T18")]
    [Trait("UseCase", "UC-AI-02 AC-2")]
    public void PredictAt_DataAfterCutoff_NotUsed()
    {
        var cutoff = LastDay.AddDays(-14);
        var before = Daily(60, day => day % 5);
        var after = new SalesHistory(
            before.BranchId, before.SkuId, before.FirstSale,
            before.DailyQuantities.ToDictionary(pair => pair.Key, pair => pair.Key > cutoff ? 1_000 : pair.Value));

        Assert.Equal(
            Forecaster.PredictAt(before, LastDay.AddDays(-59), cutoff),
            Forecaster.PredictAt(after, LastDay.AddDays(-59), cutoff));
    }

    // AC-3: WAPE của dự báo và của trung bình 7 ngày cuối phần huấn luyện, gộp trên mọi chi nhánh và SKU đủ dữ liệu.
    [Fact]
    [Trait("Scenario", "T18")]
    [Trait("UseCase", "UC-AI-02 AC-3")]
    public void Backtest_TwoSkus_WapeOfForecastAndBaseline()
    {
        var cutoff = LastDay.AddDays(-14);
        // SKU A: 46 ngày huấn luyện đều 2, rồi 14 ngày kiểm tra đều 4. Dự báo và cơ sở cùng là 2: sai 2 mỗi ngày.
        var a = Daily(60, day => day < 46 ? 2 : 4);
        // SKU B: 39 ngày 0, 7 ngày cuối huấn luyện đều 7, rồi 14 ngày kiểm tra đều 7. Cơ sở là 7: không sai.
        var b = Daily(60, day => day < 39 ? 0 : 7);
        var levelB = ExponentialSmoothing.Level(b.Series(LastDay.AddDays(-59), cutoff), 0.3m);

        var result = Forecaster.Backtest([a, b], LastDay);

        var actual = 14m * 4 + 14m * 7;
        Assert.Equal(Math.Round((14m * 2 + 14m * Math.Abs(7 - levelB)) / actual, 4), result.ForecastWape);
        Assert.Equal(Math.Round(14m * 2 / actual, 4), result.BaselineWape);
    }

    // SKU chưa đủ 14 ngày lịch sử tại mốc cắt không vào backtest, dù tới hôm qua đã đủ.
    [Fact]
    [Trait("Scenario", "T18")]
    [Trait("UseCase", "UC-AI-02 AC-2")]
    public void Backtest_SkuNewAtCutoff_Excluded()
    {
        var steady = Daily(60, _ => 3);
        var newcomer = Daily(20, _ => 100);

        Assert.Equal(Forecaster.Backtest([steady], LastDay), Forecaster.Backtest([steady, newcomer], LastDay));
        Assert.Equal(new BacktestResult(0m, 0m), Forecaster.Backtest([steady], LastDay));
    }

    // Tổng thực tế của phần kiểm tra bằng 0 thì không có dữ liệu để đánh giá.
    [Fact]
    [Trait("Scenario", "T18")]
    public void Backtest_NoActualSalesInTestPeriod_NoWape() =>
        Assert.Equal(new BacktestResult(null, null), Forecaster.Backtest([Daily(60, day => day < 46 ? 5 : 0)], LastDay));

    [Fact]
    public void ForecastRun_Failed_KeepsErrorWithoutWape()
    {
        var run = ForecastRun.Failed(RunId, DateTimeOffset.UnixEpoch, new string('x', 3000));

        Assert.Equal((ForecastRunStatus.Failed, 2000, (decimal?)null), (run.Status, run.Error!.Length, run.ForecastWape));
    }

    // Lịch sử `days` ngày tính tới hết LastDay, ngày thứ i (từ 0, cũ trước) bán quantity(i).
    private static SalesHistory Daily(int days, Func<int, int> quantity)
    {
        var first = LastDay.AddDays(1 - days);
        return new SalesHistory(Guid.NewGuid(), Guid.NewGuid(), first, Quantities(first, days, quantity));
    }

    private static Dictionary<DateOnly, int> Quantities(DateOnly first, int days, Func<int, int> quantity) =>
        Enumerable.Range(0, days).ToDictionary(day => first.AddDays(day), quantity);
}
