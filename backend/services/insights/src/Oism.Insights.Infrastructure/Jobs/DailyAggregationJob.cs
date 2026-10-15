using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oism.BuildingBlocks.Tenancy;
using Oism.Insights.Application;
using Oism.Insights.Application.Reports.RebuildDailyAggregates;
using Oism.Insights.Domain.Sales;

namespace Oism.Insights.Infrastructure.Jobs;

// UC-REP-01 AC-8: mỗi đêm dựng lại daily_aggregates của ngày hôm qua (giờ Việt Nam) cho từng tenant
// (docs/design/api/insights.md mục "Đường vào không phải HTTP").
public sealed class DailyAggregationJob(IServiceScopeFactory scopes, IClock clock, ILogger<DailyAggregationJob> logger)
{
    public const string Id = "daily-aggregates";

    public Task RunForYesterdayAsync(CancellationToken ct) => RunAsync(ReportCalendar.DateOf(clock.UtcNow).AddDays(-1), ct);

    public async Task RunAsync(DateOnly date, CancellationToken ct)
    {
        foreach (var tenantId in await ListTenantsAsync(date, ct))
        {
            try
            {
                // Mỗi tenant một scope: tenant context của chính tenant đó, một DbContext và một transaction riêng.
                await using var scope = scopes.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
                await scope.ServiceProvider.GetRequiredService<RebuildDailyAggregatesHandler>()
                    .Handle(new RebuildDailyAggregatesCommand(date), ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Transaction của tenant này đã rollback; báo cáo của tenant đó đọc sales_facts cho ngày này tới lần chạy sau.
                logger.LogError(exception, "Aggregating {Date} for tenant {TenantId} failed", date, tenantId);
            }
        }
    }

    // Một trong các chỗ được phép bỏ filter tenant: docs/architecture/multi-tenancy.md.
    private async Task<List<Guid>> ListTenantsAsync(DateOnly date, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var start = ReportCalendar.StartOf(date).ToUniversalTime();
        var end = ReportCalendar.StartOf(date.AddDays(1)).ToUniversalTime();
        return await scope.ServiceProvider.GetRequiredService<InsightsDbContext>().SalesFacts
            .IgnoreQueryFilters().AsNoTracking()
            .Where(fact => fact.ConfirmedAt >= start && fact.ConfirmedAt < end)
            .Select(fact => fact.TenantId)
            .Distinct()
            .ToListAsync(ct);
    }
}

// Đặt lịch lúc service khởi động. 17:05 UTC là 00:05 giờ Việt Nam: ngày hôm qua vừa khép lại.
// Dự báo chạy sau đó, lúc 00:30 giờ Việt Nam, trên lịch sử tính tới hết ngày hôm qua.
internal sealed class JobSchedule(IRecurringJobManager jobs) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        jobs.AddOrUpdate<DailyAggregationJob>(
            DailyAggregationJob.Id, job => job.RunForYesterdayAsync(CancellationToken.None), "5 17 * * *");
        jobs.AddOrUpdate<ForecastJob>(ForecastJob.Id, job => job.RunAsync(CancellationToken.None), "30 17 * * *");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
