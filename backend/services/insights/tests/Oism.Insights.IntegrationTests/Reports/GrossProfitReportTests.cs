using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Oism.Contracts;
using Oism.Insights.Application.Reports.GetGrossProfit;
using Oism.Insights.Domain.Sales;
using Oism.Insights.Infrastructure.Jobs;
using static Oism.Insights.IntegrationTests.TestKit;

namespace Oism.Insights.IntegrationTests.Reports;

// W4-02, UC-REP-01: GET /reports/gross-profit và job tổng hợp ngày.
public sealed class GrossProfitReportTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _branchA = Guid.NewGuid();
    private readonly Guid _branchB = Guid.NewGuid();
    private readonly Guid _skuX = Guid.NewGuid();
    private readonly Guid _skuY = Guid.NewGuid();

    // Điều kiện xong của W4-02: 300.000 − 220.000 = 80.000.
    [Fact]
    [Trait("UseCase", "UC-REP-01 AC-1")]
    [Trait("UseCase", "UC-REP-01 AC-2")]
    public async Task GrossProfit_TwoUnitsAt150kWithLockedCost110k_Revenue300kCogs220kProfit80k()
    {
        await factory.RecordSaleAsync(_tenantId, Confirmed(_branchA, Vn(2026, 10, 20), "Shopee", Line(_skuX)));

        var report = await GetAsync("from=2026-10-20&to=2026-10-20");

        Assert.Equal(new GrossProfitTotals(2, 300_000m, 220_000m, 80_000m), report.Totals);
        Assert.Empty(report.Rows);
    }

    // Lọc theo thời gian, chi nhánh, kênh, SKU kết hợp bất kỳ: chỉ dòng khớp mọi điều kiện.
    [Fact]
    [Trait("UseCase", "UC-REP-01 AC-3")]
    public async Task GrossProfit_AnyCombinationOfFilters_OnlyRowsMatchingEveryCondition()
    {
        await factory.RecordSaleAsync(_tenantId, Confirmed(_branchA, Vn(2026, 10, 20), "Shopee", Line(_skuX, 1, 100_000, costPrice: 60_000)));
        await factory.RecordSaleAsync(_tenantId, Confirmed(_branchA, Vn(2026, 10, 20), "TikTok", Line(_skuY, 1, 200_000, costPrice: 150_000)));
        await factory.RecordSaleAsync(_tenantId, Confirmed(_branchB, Vn(2026, 10, 20), "Shopee", Line(_skuX, 1, 400_000, costPrice: 300_000)));
        await factory.RecordSaleAsync(_tenantId, Confirmed(_branchA, Vn(2026, 10, 21), "Shopee", Line(_skuX, 1, 800_000, costPrice: 500_000)));

        Assert.Equal(1_500_000m, (await GetAsync("from=2026-10-20&to=2026-10-21")).Totals.Revenue);
        Assert.Equal(700_000m, (await GetAsync("from=2026-10-20&to=2026-10-20")).Totals.Revenue);
        Assert.Equal(1_100_000m, (await GetAsync($"from=2026-10-20&to=2026-10-21&branchId={_branchA}")).Totals.Revenue);
        Assert.Equal(1_300_000m, (await GetAsync("from=2026-10-20&to=2026-10-21&channel=Shopee")).Totals.Revenue);
        Assert.Equal(1_300_000m, (await GetAsync($"from=2026-10-20&to=2026-10-21&skuId={_skuX}")).Totals.Revenue);
        Assert.Equal(
            new GrossProfitTotals(1, 100_000m, 60_000m, 40_000m),
            (await GetAsync($"from=2026-10-20&to=2026-10-20&branchId={_branchA}&channel=Shopee&skuId={_skuX}")).Totals);
        Assert.Equal(new GrossProfitTotals(0, 0, 0, 0), (await GetAsync($"from=2026-10-21&to=2026-10-21&channel=TikTok")).Totals);
    }

    [Fact]
    [Trait("UseCase", "UC-REP-01 AC-1")]
    public async Task GrossProfit_GroupBy_OneRowPerGroupWithProfit()
    {
        await factory.UpsertSkuNameAsync(_tenantId, _skuX, "AO-01", "Áo thun, Đen");
        await factory.RecordSaleAsync(_tenantId, Confirmed(_branchA, Vn(2026, 10, 20), "Shopee", Line(_skuX, 1, 100_000, costPrice: 60_000)));
        await factory.RecordSaleAsync(_tenantId, Confirmed(_branchB, Vn(2026, 10, 21), "POS", Line(_skuY, 2, 300_000, costPrice: 200_000)));

        var byDay = await GetAsync("from=2026-10-20&to=2026-10-21&groupBy=day");
        Assert.Equal(["2026-10-20", "2026-10-21"], byDay.Rows.Select(row => row.Key));
        Assert.Equal(new GrossProfitTotals(3, 700_000m, 460_000m, 240_000m), byDay.Totals);

        var byBranch = await GetAsync("from=2026-10-20&to=2026-10-21&groupBy=branch");
        Assert.Equal([_branchB.ToString(), _branchA.ToString()], byBranch.Rows.Select(row => row.Key));

        var byChannel = await GetAsync("from=2026-10-20&to=2026-10-21&groupBy=channel");
        Assert.Equal(("POS", 600_000m, 400_000m, 200_000m), (byChannel.Rows[0].Key, byChannel.Rows[0].Revenue, byChannel.Rows[0].Cogs, byChannel.Rows[0].GrossProfit));

        var bySku = await GetAsync("from=2026-10-20&to=2026-10-21&groupBy=sku");
        var x = Assert.Single(bySku.Rows, row => row.Key == _skuX.ToString());
        Assert.Equal(("AO-01", "Áo thun, Đen", 1, 40_000m), (x.SkuCode, x.Name, x.Quantity, x.GrossProfit));
    }

    // Ngày cắt theo giờ Việt Nam: 23:30 ngày 20 giờ Việt Nam là 16:30 UTC, 00:30 ngày 21 giờ Việt Nam là 17:30 UTC ngày 20.
    [Fact]
    [Trait("UseCase", "UC-REP-01 AC-3")]
    public async Task GrossProfit_DayBoundary_CutByVietnamTime()
    {
        await factory.RecordSaleAsync(_tenantId, Confirmed(_branchA, Vn(2026, 10, 20, 23, 30), "Shopee", Line(_skuX, 1, 100_000)));
        await factory.RecordSaleAsync(_tenantId, Confirmed(_branchA, Vn(2026, 10, 21, 0, 30), "Shopee", Line(_skuX, 1, 200_000)));

        Assert.Equal(100_000m, (await GetAsync("from=2026-10-20&to=2026-10-20")).Totals.Revenue);
        var byDay = await GetAsync("from=2026-10-20&to=2026-10-21&groupBy=day");
        Assert.Equal([("2026-10-20", 100_000m), ("2026-10-21", 200_000m)], byDay.Rows.Select(row => (row.Key, row.Revenue)));
    }

    // T10: giá vốn của đơn đã vào báo cáo là giá đã chốt; lô nhập mới làm avg_cost thành 120.000 không đổi lợi nhuận cũ.
    [Fact]
    [Trait("Scenario", "T10")]
    [Trait("UseCase", "UC-REP-01 AC-4")]
    public async Task GrossProfit_NewPurchaseRaisesAvgCost_OldOrderProfitUnchanged()
    {
        await factory.ApplyStockChangeAsync(_tenantId, new StockChanged(_branchA, _skuX, 10, 0, 10, 110_000, null, Version: 1));
        await factory.RecordSaleAsync(_tenantId, Confirmed(_branchA, Vn(2026, 10, 20), "POS", Line(_skuX, 2, 150_000, costPrice: 110_000)));
        await factory.ApplyStockChangeAsync(_tenantId, new StockChanged(_branchA, _skuX, 8, 0, 8, 110_000, null, Version: 2));

        // Nhập lô mới: avg_cost của số dư thành 120.000.
        await factory.ApplyStockChangeAsync(_tenantId, new StockChanged(_branchA, _skuX, 18, 0, 18, 120_000, null, Version: 3));

        Assert.Equal(new GrossProfitTotals(2, 300_000m, 220_000m, 80_000m), (await GetAsync("from=2026-10-20&to=2026-10-20")).Totals);
    }

    [Theory]
    [Trait("UseCase", "UC-REP-01 AC-6")]
    [InlineData("Staff")]
    [InlineData("Cashier")]
    public async Task GrossProfit_NotOwner_Returns403(string role)
    {
        var response = await factory.CreateClient(role, _tenantId).GetAsync("/reports/gross-profit?from=2026-10-20&to=2026-10-20");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("to=2026-10-20")]
    [InlineData("from=2026-10-21&to=2026-10-20")]
    [InlineData("from=2026-10-20&to=2026-10-20&groupBy=month")]
    [InlineData("from=hôm-qua&to=2026-10-20")]
    public async Task GrossProfit_InvalidQuery_Returns400(string query)
    {
        var response = await factory.CreateClient("Owner", _tenantId).GetAsync($"/reports/gross-profit?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    // NFR-TENANT-01: số liệu của tenant khác không lẫn vào báo cáo.
    [Fact]
    [Trait("Scenario", "T14")]
    public async Task GrossProfit_OtherTenantSales_NotIncluded()
    {
        await factory.RecordSaleAsync(Guid.NewGuid(), Confirmed(_branchA, Vn(2026, 10, 20), "Shopee", Line(_skuX)));

        Assert.Equal(new GrossProfitTotals(0, 0, 0, 0), (await GetAsync("from=2026-10-20&to=2026-10-20")).Totals);
    }

    // AC-8: job dựng lại daily_aggregates của một ngày; chạy lại cho cùng ngày cho ra cùng kết quả, không cộng dồn.
    [Fact]
    [Trait("UseCase", "UC-REP-01 AC-8")]
    public async Task DailyAggregation_RunTwiceForSameDay_SameAggregatesAndReport()
    {
        var day = new DateOnly(2026, 9, 15);
        await factory.RecordSaleAsync(_tenantId, Confirmed(_branchA, Vn(2026, 9, 15, 9), "Shopee", Line(_skuX, 2, 150_000)));
        await factory.RecordSaleAsync(_tenantId, Confirmed(_branchA, Vn(2026, 9, 15, 20), "Shopee", Line(_skuX, 1, 150_000)));
        await factory.RecordSaleAsync(_tenantId, Confirmed(_branchB, Vn(2026, 9, 15, 23, 59), "POS", Line(_skuY, 1, 90_000, costPrice: 50_000)));
        // Ngày khác, không thuộc lần tổng hợp này.
        await factory.RecordSaleAsync(_tenantId, Confirmed(_branchA, Vn(2026, 9, 16, 0, 1), "Shopee", Line(_skuX, 1, 1_000)));

        var job = factory.Services.GetRequiredService<DailyAggregationJob>();
        await job.RunAsync(day, default);
        var first = await AggregatesAsync();
        await job.RunAsync(day, default);
        var second = await AggregatesAsync();

        Assert.Equal(
            [(day, _branchA, "Shopee", _skuX, 3, 450_000m, 330_000m), (day, _branchB, "POS", _skuY, 1, 90_000m, 50_000m)],
            first);
        Assert.Equal(first, second);
        Assert.Equal(new GrossProfitTotals(4, 540_000m, 380_000m, 160_000m), (await GetAsync("from=2026-09-15&to=2026-09-15")).Totals);
    }

    // Ngày đã tổng hợp đọc từ daily_aggregates; đơn tới trễ cho ngày đó bỏ bản tổng hợp để báo cáo vẫn đủ.
    [Fact]
    [Trait("UseCase", "UC-REP-01 AC-8")]
    public async Task DailyAggregation_LateOrderForAggregatedDay_ReportIncludesIt()
    {
        var day = new DateOnly(2026, 9, 15);
        await factory.RecordSaleAsync(_tenantId, Confirmed(_branchA, Vn(2026, 9, 15), "Shopee", Line(_skuX, 2, 150_000)));
        await factory.Services.GetRequiredService<DailyAggregationJob>().RunAsync(day, default);
        Assert.NotEmpty(await AggregatesAsync());

        await factory.RecordSaleAsync(_tenantId, Confirmed(_branchA, Vn(2026, 9, 15, 22), "TikTok", Line(_skuY, 1, 100_000, costPrice: 70_000)));

        Assert.Empty(await AggregatesAsync());
        var report = await GetAsync("from=2026-09-15&to=2026-09-15&groupBy=channel");
        Assert.Equal(new GrossProfitTotals(3, 400_000m, 290_000m, 110_000m), report.Totals);
        Assert.Equal(["Shopee", "TikTok"], report.Rows.Select(row => row.Key));
    }

    // AC-7 (NFR-PERF-01): 100.000 dòng bán của một tenant trong 30 ngày, chưa qua tổng hợp (trường hợp chậm nhất), báo cáo
    // nhóm theo SKU trả về dưới 2 giây. Đo đầy đủ bằng k6 trên gói Compose ở T17.
    [Fact]
    [Trait("UseCase", "UC-REP-01 AC-7")]
    public async Task GrossProfit_100kSalesFacts_RespondsUnder2Seconds()
    {
        await factory.QueryAsync(_tenantId, db => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO insights.sales_facts
                (order_item_id, tenant_id, order_id, branch_id, sku_id, channel, quantity, revenue, cogs, confirmed_at)
            SELECT gen_random_uuid(), {_tenantId}, gen_random_uuid(),
                   CASE WHEN i % 2 = 0 THEN {_branchA} ELSE {_branchB} END,
                   md5('sku' || (i % 500))::uuid,
                   (ARRAY['Shopee', 'TikTok', 'Lazada', 'POS'])[1 + i % 4],
                   1 + i % 3, (1 + i % 3) * 150000, (1 + i % 3) * 110000,
                   {Vn(2026, 8, 1, 0)} + (i % 30) * interval '1 day' + (i % 86400) * interval '1 second'
            FROM generate_series(1, 100000) AS i
            """));
        var client = factory.CreateClient("Owner", _tenantId);
        await client.GetAsync("/reports/gross-profit?from=2026-08-01&to=2026-08-01");

        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetAsync("/reports/gross-profit?from=2026-08-01&to=2026-08-30&groupBy=sku");
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = (await response.Content.ReadFromJsonAsync<GrossProfitReport>())!;
        Assert.Equal(500, report.Rows.Count);
        Assert.Equal(200_000, report.Totals.Quantity);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"report took {stopwatch.Elapsed.TotalMilliseconds} ms");
    }

    private async Task<GrossProfitReport> GetAsync(string query)
    {
        var response = await factory.CreateClient("Owner", _tenantId).GetAsync($"/reports/gross-profit?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GrossProfitReport>())!;
    }

    private Task<List<(DateOnly, Guid, string, Guid, int, decimal, decimal)>> AggregatesAsync() =>
        factory.QueryAsync(_tenantId, async db => (await db.DailyAggregates.AsNoTracking().ToListAsync())
            .OrderBy(aggregate => aggregate.Revenue).Reverse()
            .Select(aggregate => (aggregate.Date, aggregate.BranchId, aggregate.Channel, aggregate.SkuId, aggregate.Quantity,
                aggregate.Revenue, aggregate.Cogs))
            .ToList());
}
