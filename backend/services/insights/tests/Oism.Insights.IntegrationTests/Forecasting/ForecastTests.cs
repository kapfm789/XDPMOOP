using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Oism.Contracts;
using Oism.Insights.Api.Controllers;
using Oism.Insights.Application;
using Oism.Insights.Application.Forecasting;
using Oism.Insights.Application.Forecasting.GetReorderSuggestions;
using Oism.Insights.Application.Forecasting.RunForecast;
using Oism.Insights.Domain.Forecasting;
using Oism.Insights.Domain.Sales;
using Oism.Insights.Infrastructure.Jobs;
using static Oism.Insights.IntegrationTests.TestKit;

namespace Oism.Insights.IntegrationTests.Forecasting;

// W4-05, UC-AI-01, UC-AI-02: POST /forecast/run, GET /forecast/reorder-suggestions và job dự báo đêm.
public sealed class ForecastTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _branchA = Guid.NewGuid();
    private readonly Guid _branchB = Guid.NewGuid();
    private readonly Guid _skuX = Guid.NewGuid();
    private readonly Guid _skuNew = Guid.NewGuid();
    private readonly Guid _skuIdle = Guid.NewGuid();

    // T18 trên job thật: 60 ngày lịch sử bán, chạy dự báo, lần chạy lưu thời điểm, phương pháp, trạng thái và hai WAPE.
    [Fact]
    [Trait("Scenario", "T18")]
    [Trait("UseCase", "UC-AI-02 AC-1")]
    [Trait("UseCase", "UC-AI-02 AC-3")]
    [Trait("UseCase", "UC-AI-01 AC-1")]
    [Trait("UseCase", "UC-AI-01 AC-4")]
    public async Task RunForecast_60DaysOfSales_ResultPerBranchAndSkuWithBacktest()
    {
        await factory.UpsertSkuNameAsync(_tenantId, _skuX, "AO-01", "Áo thun, Đen");
        await SellDailyAsync(_branchA, _skuX, days: 60, day => 3 + day % 3);
        await SellDailyAsync(_branchB, _skuX, days: 60, _ => 1);
        await StockAsync(_branchA, _skuX, available: 10);

        var runId = await RunAsync();
        var suggestions = await GetAsync();

        var run = suggestions.Run!;
        Assert.Equal((runId, "Succeeded", DemandForecaster.Method), (run.Id, run.Status, run.Method));
        Assert.NotNull(run.ForecastWape);
        Assert.NotNull(run.BaselineWape);
        Assert.Equal(run, suggestions.LastSucceededRun);
        Assert.Equal(2, suggestions.Total);

        var a = Assert.Single(suggestions.Items, item => item.BranchId == _branchA);
        Assert.Equal(("AO-01", "Áo thun, Đen", true, 10), (a.SkuCode, a.Name, a.HasEnoughData, a.Available));
        Assert.InRange(a.ForecastQty!.Value, 3m * 14, 5m * 14);
        Assert.Equal((int)Math.Ceiling(a.ForecastQty.Value / 14 * 10) - 10, a.SuggestedReorderQty);
        var b = Assert.Single(suggestions.Items, item => item.BranchId == _branchB);
        Assert.Equal((14m, 0, 10), (b.ForecastQty, b.Available, b.SuggestedReorderQty));
    }

    // T19: SKU mới 5 ngày lịch sử và SKU có tồn mà chưa bán đều hiện chưa đủ dữ liệu, không có con số; tồn không đổi.
    [Fact]
    [Trait("Scenario", "T19")]
    [Trait("UseCase", "UC-AI-01 AC-2")]
    [Trait("UseCase", "UC-AI-01 AC-3")]
    public async Task RunForecast_SkuWithFewDaysOrNoSales_NotEnoughDataAndStockUnchanged()
    {
        await SellDailyAsync(_branchA, _skuX, days: 30, _ => 2);
        await SellDailyAsync(_branchA, _skuNew, days: 5, _ => 50);
        await StockAsync(_branchA, _skuIdle, available: 7);
        var stockBefore = await SnapshotsAsync();

        await RunAsync();
        var suggestions = await GetAsync();

        Assert.Equal([_skuX], suggestions.Items.Where(item => item.HasEnoughData).Select(item => item.SkuId));
        foreach (var skuId in new[] { _skuNew, _skuIdle })
        {
            var item = Assert.Single(suggestions.Items, item => item.SkuId == skuId);
            Assert.Equal((false, (decimal?)null, (int?)null), (item.HasEnoughData, item.ForecastQty, item.SuggestedReorderQty));
        }

        // Đề xuất chỉ là khuyến nghị: insights không có phiếu nhập hay sổ, và tồn nó giữ không đổi.
        Assert.Equal(stockBefore, await SnapshotsAsync());
    }

    // T19: lần chạy lỗi ghi Failed kèm lỗi; màn hình vẫn có kết quả của lần thành công trước.
    [Fact]
    [Trait("Scenario", "T19")]
    [Trait("UseCase", "UC-AI-01 AC-5")]
    public async Task RunForecast_FailingRun_RecordedFailedAndPreviousResultsStillShown()
    {
        await SellDailyAsync(_branchA, _skuX, days: 30, _ => 2);
        var succeededId = await RunAsync();

        var failedId = await RunWithSourceAsync(new FailingSource());
        var suggestions = await GetAsync();

        Assert.Equal((failedId, "Failed", "broken source"), (suggestions.Run!.Id, suggestions.Run.Status, suggestions.Run.Error));
        Assert.Null(suggestions.Run.ForecastWape);
        Assert.Equal(succeededId, suggestions.LastSucceededRun!.Id);
        Assert.Equal(_skuX, Assert.Single(suggestions.Items).SkuId);
    }

    // Lỗi giữa transaction, sau khi đã thêm lần chạy và một phần kết quả: không kết quả nào còn lại, chỉ một dòng Failed.
    [Fact]
    [Trait("Scenario", "T19")]
    [Trait("UseCase", "UC-AI-01 AC-5")]
    public async Task RunForecast_FailureMidTransaction_NoPartialResults()
    {
        var history = new SalesHistory(_branchA, _skuX, null, new Dictionary<DateOnly, int>());

        var runId = await RunWithSourceAsync(new FixedSource([history, history]));

        var (runs, results) = await factory.QueryAsync(_tenantId, async db => (
            await db.ForecastRuns.AsNoTracking().Select(run => new { run.Id, run.Status }).ToListAsync(),
            await db.ForecastResults.AsNoTracking().CountAsync()));
        Assert.Equal((runId, ForecastRunStatus.Failed), (Assert.Single(runs).Id, runs[0].Status));
        Assert.Equal(0, results);
    }

    // Lọc theo chi nhánh, phân trang theo khuôn chung; chưa có lần chạy nào thì không có dòng.
    [Fact]
    [Trait("UseCase", "UC-AI-01 AC-1")]
    public async Task ReorderSuggestions_BranchFilterAndPaging()
    {
        var empty = await GetAsync();
        Assert.Equal(((ForecastRunDto?)null, 0), (empty.Run, empty.Total));

        await SellDailyAsync(_branchA, _skuX, days: 20, _ => 1);
        await SellDailyAsync(_branchA, _skuNew, days: 20, _ => 9);
        await SellDailyAsync(_branchB, _skuX, days: 20, _ => 1);
        await RunAsync();

        var page = await GetAsync($"?branchId={_branchA}&page=1&pageSize=1");
        Assert.Equal((2, 1, 1), (page.Total, page.Page, page.PageSize));
        Assert.Equal(_skuNew, Assert.Single(page.Items).SkuId);
    }

    // AC-4: job đêm chạy cho từng tenant trong tenant context của nó; dự báo của tenant này không dùng số của tenant khác.
    [Fact]
    [Trait("UseCase", "UC-AI-02 AC-4")]
    [Trait("Scenario", "T14")]
    public async Task ForecastJob_TwoTenants_EachUsesOnlyItsOwnSales()
    {
        var other = Guid.NewGuid();
        await SellDailyAsync(_branchA, _skuX, days: 20, _ => 2);
        for (var day = 1; day <= 20; day++)
            await factory.RecordSaleAsync(other, Confirmed(_branchA, DayAgo(day), "POS", Line(_skuX, quantity: 100)));

        await factory.Services.GetRequiredService<ForecastJob>().RunAsync(default);

        var mine = Assert.Single((await GetAsync()).Items);
        Assert.Equal(28m, mine.ForecastQty);
        var theirs = await factory.CreateClient("Owner", other).GetFromJsonAsync<ReorderSuggestions>("/forecast/reorder-suggestions");
        Assert.Equal(1_400m, Assert.Single(theirs!.Items).ForecastQty);
    }

    [Theory]
    [InlineData("Staff")]
    [InlineData("Cashier")]
    public async Task Forecast_NotOwner_Returns403(string role)
    {
        var client = factory.CreateClient(role, _tenantId);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/forecast/reorder-suggestions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/forecast/run", null)).StatusCode);
    }

    // 12:00 giờ Việt Nam của ngày cách hôm nay `days` ngày.
    private static DateTimeOffset DayAgo(int days) =>
        ReportCalendar.StartOf(ReportCalendar.DateOf(DateTimeOffset.UtcNow).AddDays(-days)).AddHours(12).ToUniversalTime();

    // Mỗi ngày trong `days` ngày tính tới hết hôm qua một đơn; ngày thứ i (từ 0, cũ trước) bán quantity(i).
    private async Task SellDailyAsync(Guid branchId, Guid skuId, int days, Func<int, int> quantity)
    {
        for (var i = 0; i < days; i++)
        {
            await factory.RecordSaleAsync(
                _tenantId, Confirmed(branchId, DayAgo(days - i), "POS", Line(skuId, quantity: quantity(i), unitPrice: 1, costPrice: 1)));
        }
    }

    private Task StockAsync(Guid branchId, Guid skuId, int available) =>
        factory.ApplyStockChangeAsync(_tenantId, new StockChanged(branchId, skuId, available, 0, available, 1_000, null, Version: 1));

    private Task<List<(Guid, Guid, int, int, long)>> SnapshotsAsync() =>
        factory.QueryAsync(_tenantId, async db => (await db.StockSnapshots.AsNoTracking().ToListAsync())
            .Select(snapshot => (snapshot.BranchId, snapshot.SkuId, snapshot.OnHand, snapshot.Available, snapshot.Version))
            .OrderBy(row => row.SkuId)
            .ToList());

    private async Task<Guid> RunAsync()
    {
        var response = await factory.CreateClient("Owner", _tenantId).PostAsync("/forecast/run", null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ForecastRunAccepted>())!.RunId;
    }

    // Cùng handler, cùng transaction và repository thật, chỉ thay nguồn lịch sử bán để cắm lỗi.
    private Task<Guid> RunWithSourceAsync(ISalesHistorySource source) =>
        factory.InTenantAsync(_tenantId, services => new RunForecastHandler(
                services.GetRequiredService<IUnitOfWork>(), source, services.GetRequiredService<IForecastRepository>(),
                services.GetRequiredService<ForecastSettings>(), services.GetRequiredService<IClock>())
            .Handle(new RunForecastCommand(), default));

    private async Task<ReorderSuggestions> GetAsync(string query = "")
    {
        var response = await factory.CreateClient("Owner", _tenantId).GetAsync($"/forecast/reorder-suggestions{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ReorderSuggestions>())!;
    }

    private sealed class FailingSource : ISalesHistorySource
    {
        public Task<IReadOnlyList<SalesHistory>> LoadAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
            throw new InvalidOperationException("broken source");

        public Task<IReadOnlyDictionary<(Guid BranchId, Guid SkuId), int>> LoadAvailableAsync(CancellationToken ct) =>
            throw new InvalidOperationException("broken source");
    }

    // Trả hai lần cùng một cặp chi nhánh và SKU: kết quả thứ hai trùng khóa chính, lỗi xảy ra sau khi đã thêm lần chạy.
    private sealed class FixedSource(IReadOnlyList<SalesHistory> histories) : ISalesHistorySource
    {
        public Task<IReadOnlyList<SalesHistory>> LoadAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
            Task.FromResult(histories);

        public Task<IReadOnlyDictionary<(Guid BranchId, Guid SkuId), int>> LoadAvailableAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<(Guid BranchId, Guid SkuId), int>>(new Dictionary<(Guid, Guid), int>());
    }
}
