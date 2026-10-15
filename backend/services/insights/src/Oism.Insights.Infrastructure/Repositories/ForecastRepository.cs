using Microsoft.EntityFrameworkCore;
using Oism.Insights.Application.Forecasting;
using Oism.Insights.Domain.Forecasting;
using Oism.Insights.Domain.Sales;

namespace Oism.Insights.Infrastructure.Repositories;

// Đầu vào và đầu ra của dự báo trong schema insights. Chỉ đọc sales_facts và stock_snapshots; không đụng core (D-14).
internal sealed class ForecastRepository(InsightsDbContext db) : ISalesHistorySource, IForecastRepository
{
    public async Task<IReadOnlyList<SalesHistory>> LoadAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        // Npgsql chỉ nhận timestamptz ở UTC.
        var start = ReportCalendar.StartOf(from).ToUniversalTime();
        var end = ReportCalendar.StartOf(to.AddDays(1)).ToUniversalTime();

        var firstSales = await db.SalesFacts.AsNoTracking()
            .Where(fact => fact.ConfirmedAt < end)
            .GroupBy(fact => new { fact.BranchId, fact.SkuId })
            .Select(group => new { group.Key.BranchId, group.Key.SkuId, First = group.Min(fact => fact.ConfirmedAt) })
            .ToListAsync(ct);

        var daily = await db.SalesFacts.AsNoTracking()
            .Where(fact => fact.ConfirmedAt >= start && fact.ConfirmedAt < end)
            .Select(fact => new
            {
                fact.BranchId,
                fact.SkuId,
                // Dịch thành (confirmed_at AT TIME ZONE 'Asia/Ho_Chi_Minh')::date.
                Date = DateOnly.FromDateTime(
                    TimeZoneInfo.ConvertTimeBySystemTimeZoneId(fact.ConfirmedAt.UtcDateTime, ReportCalendar.TimeZone)),
                fact.Quantity,
            })
            .GroupBy(line => new { line.BranchId, line.SkuId, line.Date })
            .Select(group => new { group.Key.BranchId, group.Key.SkuId, group.Key.Date, Quantity = group.Sum(line => line.Quantity) })
            .ToListAsync(ct);

        var stocked = await db.StockSnapshots.AsNoTracking()
            .Select(snapshot => new { snapshot.BranchId, snapshot.SkuId })
            .ToListAsync(ct);

        // Mọi cặp chi nhánh và SKU từng bán hoặc đang có số dư; cặp chưa bán lần nào có FirstSale null.
        var first = firstSales.ToDictionary(row => (row.BranchId, row.SkuId), row => ReportCalendar.DateOf(row.First));
        var quantities = daily
            .GroupBy(row => (row.BranchId, row.SkuId))
            .ToDictionary(group => group.Key, group => (IReadOnlyDictionary<DateOnly, int>)group.ToDictionary(row => row.Date, row => row.Quantity));
        return first.Keys
            .Union(stocked.Select(row => (row.BranchId, row.SkuId)))
            .Select(key => new SalesHistory(
                key.BranchId, key.SkuId, first.TryGetValue(key, out var day) ? day : null,
                quantities.GetValueOrDefault(key) ?? new Dictionary<DateOnly, int>()))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<(Guid BranchId, Guid SkuId), int>> LoadAvailableAsync(CancellationToken ct) =>
        await db.StockSnapshots.AsNoTracking()
            .ToDictionaryAsync(snapshot => (snapshot.BranchId, snapshot.SkuId), snapshot => snapshot.Available, ct);

    public void Add(ForecastRun run) => db.Add(run);

    public void AddRange(IEnumerable<ForecastResult> results) => db.AddRange(results);
}
