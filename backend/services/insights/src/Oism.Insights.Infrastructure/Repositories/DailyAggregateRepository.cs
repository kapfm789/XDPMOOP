using Microsoft.EntityFrameworkCore;
using Oism.Insights.Application.Reports;
using Oism.Insights.Domain.Sales;

namespace Oism.Insights.Infrastructure.Repositories;

internal sealed class DailyAggregateRepository(InsightsDbContext db) : IDailyAggregateRepository
{
    public async Task<IReadOnlyList<DailySalesTotal>> SumSalesAsync(DateOnly date, CancellationToken ct)
    {
        // Npgsql chỉ nhận timestamptz ở UTC.
        var start = ReportCalendar.StartOf(date).ToUniversalTime();
        var end = ReportCalendar.StartOf(date.AddDays(1)).ToUniversalTime();
        return await db.SalesFacts.AsNoTracking()
            .Where(fact => fact.ConfirmedAt >= start && fact.ConfirmedAt < end)
            .GroupBy(fact => new { fact.BranchId, fact.Channel, fact.SkuId })
            .Select(group => new DailySalesTotal(
                group.Key.BranchId, group.Key.Channel, group.Key.SkuId,
                group.Sum(fact => fact.Quantity), group.Sum(fact => fact.Revenue), group.Sum(fact => fact.Cogs)))
            .ToListAsync(ct);
    }

    public Task RemoveAsync(DateOnly date, CancellationToken ct) =>
        db.DailyAggregates.Where(aggregate => aggregate.Date == date).ExecuteDeleteAsync(ct);

    public void Add(DailyAggregate aggregate) => db.Add(aggregate);
}
