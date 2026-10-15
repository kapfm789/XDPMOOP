using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Oism.BuildingBlocks.Tenancy;
using Oism.Insights.Application.Forecasting.RunForecast;

namespace Oism.Insights.Infrastructure.Jobs;

// UC-AI-02: mỗi đêm chạy dự báo cho từng tenant (docs/design/forecast.md mục "Chạy").
public sealed class ForecastJob(IServiceScopeFactory scopes, ILogger<ForecastJob> logger)
{
    public const string Id = "forecast";

    public async Task RunAsync(CancellationToken ct)
    {
        foreach (var tenantId in await ListTenantsAsync(ct))
        {
            try
            {
                // Mỗi tenant một scope: tenant context của chính tenant đó, một DbContext và transaction riêng,
                // nên dự báo của một tenant chỉ dùng dữ liệu của tenant đó (UC-AI-02 AC-4).
                await using var scope = scopes.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
                await scope.ServiceProvider.GetRequiredService<RunForecastHandler>().Handle(new RunForecastCommand(), ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Handler đã tự ghi lần chạy Failed khi tính lỗi; tới đây là lỗi cả khi ghi trạng thái đó.
                logger.LogError(exception, "Forecast for tenant {TenantId} failed", tenantId);
            }
        }
    }

    // Một trong các chỗ được phép bỏ filter tenant: docs/architecture/multi-tenancy.md.
    private async Task<List<Guid>> ListTenantsAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InsightsDbContext>();
        var withSales = await db.SalesFacts.IgnoreQueryFilters().AsNoTracking()
            .Select(fact => fact.TenantId).Distinct().ToListAsync(ct);
        var withStock = await db.StockSnapshots.IgnoreQueryFilters().AsNoTracking()
            .Select(snapshot => snapshot.TenantId).Distinct().ToListAsync(ct);
        return withSales.Union(withStock).ToList();
    }
}
