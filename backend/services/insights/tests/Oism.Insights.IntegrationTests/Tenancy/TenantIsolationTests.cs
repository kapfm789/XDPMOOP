using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Oism.Insights.Application.Reports.GetGrossProfit;
using Oism.Insights.Infrastructure.Jobs;
using static Oism.Insights.IntegrationTests.TestKit;

namespace Oism.Insights.IntegrationTests.Tenancy;

// W5-02, phía job: job tổng hợp ngày lặp qua từng tenant, mỗi vòng một tenant context (docs/architecture/multi-tenancy.md).
// Báo cáo, cảnh báo, dự báo, consumer và SignalR có test cách ly ở thư mục của từng phần.
public sealed class TenantIsolationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    [Trait("Scenario", "T14")]
    [Trait("UseCase", "UC-REP-01 AC-8")]
    public async Task DailyAggregation_TwoTenantsSellingTheSameSkuOnTheSameDay_EachGetsOnlyItsOwnTotals()
    {
        var (tenantA, tenantB) = (Guid.NewGuid(), Guid.NewGuid());
        var (branch, sku) = (Guid.NewGuid(), Guid.NewGuid());
        var day = new DateOnly(2026, 9, 10);
        await factory.RecordSaleAsync(tenantA, Confirmed(branch, Vn(2026, 9, 10), "POS", Line(sku, 1, 100_000, costPrice: 60_000)));
        await factory.RecordSaleAsync(tenantB, Confirmed(branch, Vn(2026, 9, 10), "POS", Line(sku, 5, 100_000, costPrice: 60_000)));

        await factory.Services.GetRequiredService<DailyAggregationJob>().RunAsync(day, default);

        Assert.Equal((tenantA, 1, 100_000m), Single(await AggregatesAsync(tenantA)));
        Assert.Equal((tenantB, 5, 500_000m), Single(await AggregatesAsync(tenantB)));
        var report = await factory.CreateClient("Owner", tenantA)
            .GetFromJsonAsync<GrossProfitReport>("/reports/gross-profit?from=2026-09-10&to=2026-09-10");
        Assert.Equal(new GrossProfitTotals(1, 100_000m, 60_000m, 40_000m), report!.Totals);
    }

    private Task<List<(Guid TenantId, int Quantity, decimal Revenue)>> AggregatesAsync(Guid tenantId) =>
        factory.QueryAsync(tenantId, async db => (await db.DailyAggregates.AsNoTracking().ToListAsync())
            .Select(aggregate => (aggregate.TenantId, aggregate.Quantity, aggregate.Revenue))
            .ToList());

    private static (Guid, int, decimal) Single(List<(Guid TenantId, int Quantity, decimal Revenue)> rows) => Assert.Single(rows);
}
