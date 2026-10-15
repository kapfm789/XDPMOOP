using Microsoft.EntityFrameworkCore;
using Oism.Insights.Application.Reports;
using Oism.Insights.Application.Reports.GetGrossProfit;
using Oism.Insights.Application.Reports.GetInventoryValue;
using Oism.Insights.Application.Reports.GetSlowMovers;
using Oism.Insights.Application.Reports.GetTopSellers;
using Oism.Insights.Domain.References;
using Oism.Insights.Domain.Sales;

namespace Oism.Insights.Infrastructure.Repositories;

// Chỉ đọc. Mã và tên SKU lấy từ sku_names của chính schema insights.
internal sealed class ReportQueries(InsightsDbContext db) : IReportQueries
{
    public async Task<IReadOnlyList<GrossProfitRow>> SumGrossProfitAsync(GrossProfitFilter filter, CancellationToken ct)
    {
        // docs/design/data-model/insights.md: ngày đã có daily_aggregates đọc từ đó, các ngày còn lại (hôm nay, ngày job
        // chưa chạy, ngày có đơn tới trễ) đọc thẳng sales_facts. Một ngày chỉ đọc từ một trong hai nguồn.
        var aggregatedDates = await db.DailyAggregates.AsNoTracking()
            .Where(aggregate => aggregate.Date >= filter.From && aggregate.Date <= filter.To)
            .Select(aggregate => aggregate.Date)
            .Distinct()
            .ToListAsync(ct);

        // Npgsql chỉ nhận timestamptz ở UTC.
        var start = ReportCalendar.StartOf(filter.From).ToUniversalTime();
        var end = ReportCalendar.StartOf(filter.To.AddDays(1)).ToUniversalTime();
        var facts = db.SalesFacts.AsNoTracking()
            .Where(fact => fact.ConfirmedAt >= start && fact.ConfirmedAt < end)
            .Select(fact => new SaleLine
            {
                // Dịch thành (confirmed_at AT TIME ZONE 'Asia/Ho_Chi_Minh')::date.
                Date = DateOnly.FromDateTime(
                    TimeZoneInfo.ConvertTimeBySystemTimeZoneId(fact.ConfirmedAt.UtcDateTime, ReportCalendar.TimeZone)),
                BranchId = fact.BranchId,
                Channel = fact.Channel,
                SkuId = fact.SkuId,
                Quantity = fact.Quantity,
                Revenue = fact.Revenue,
                Cogs = fact.Cogs,
            })
            .Where(line => !aggregatedDates.Contains(line.Date));

        var aggregates = db.DailyAggregates.AsNoTracking()
            .Where(aggregate => aggregate.Date >= filter.From && aggregate.Date <= filter.To)
            .Select(aggregate => new SaleLine
            {
                Date = aggregate.Date,
                BranchId = aggregate.BranchId,
                Channel = aggregate.Channel,
                SkuId = aggregate.SkuId,
                Quantity = aggregate.Quantity,
                Revenue = aggregate.Revenue,
                Cogs = aggregate.Cogs,
            });

        var buckets = (await SumAsync(Where(facts, filter), filter.GroupBy, ct))
            .Concat(await SumAsync(Where(aggregates, filter), filter.GroupBy, ct))
            .GroupBy(bucket => (bucket.Date, bucket.BranchId, bucket.Channel, bucket.SkuId))
            .Select(group => new Bucket
            {
                Date = group.Key.Date,
                BranchId = group.Key.BranchId,
                Channel = group.Key.Channel,
                SkuId = group.Key.SkuId,
                Quantity = group.Sum(bucket => bucket.Quantity),
                Revenue = group.Sum(bucket => bucket.Revenue),
                Cogs = group.Sum(bucket => bucket.Cogs),
            })
            .ToList();

        var skuIds = buckets.Where(bucket => bucket.SkuId is not null).Select(bucket => bucket.SkuId!.Value).ToList();
        var skus = await db.SkuNames.AsNoTracking().Where(sku => skuIds.Contains(sku.SkuId)).ToDictionaryAsync(sku => sku.SkuId, ct);

        var rows = buckets.Select(bucket =>
        {
            var sku = bucket.SkuId is { } skuId ? skus.GetValueOrDefault(skuId) : null;
            var key = bucket.Date?.ToString("yyyy-MM-dd") ?? bucket.BranchId?.ToString() ?? bucket.Channel ?? bucket.SkuId?.ToString() ?? "";
            return new GrossProfitRow(
                key, sku?.SkuCode, sku?.Name, bucket.Quantity, bucket.Revenue, bucket.Cogs, bucket.Revenue - bucket.Cogs);
        });

        return filter.GroupBy == GrossProfitGroupBy.Day
            ? rows.OrderBy(row => row.Key).ToList()
            : rows.OrderByDescending(row => row.Revenue).ThenBy(row => row.Key).ToList();
    }

    public async Task<InventoryValueReport> ListInventoryValueAsync(GetInventoryValueQuery query, CancellationToken ct)
    {
        // ADR-0008: giá trị tồn là on_hand × avg_cost; viết lại công thức của StockSnapshot.Value để database tính và xếp.
        var rows =
            from snapshot in db.StockSnapshots.AsNoTracking()
            where snapshot.OnHand > 0 && (query.BranchId == null || snapshot.BranchId == query.BranchId)
            join sku in db.SkuNames.AsNoTracking() on snapshot.SkuId equals sku.SkuId into skus
            from sku in skus.DefaultIfEmpty()
            select new
            {
                snapshot.BranchId,
                snapshot.SkuId,
                sku.SkuCode,
                sku.Name,
                snapshot.OnHand,
                snapshot.AvgCost,
                Value = snapshot.OnHand * snapshot.AvgCost,
            };

        // Không được xem giá vốn thì cũng không xếp theo giá trị, để thứ tự không lộ giá vốn.
        var includeCost = query.IncludeCost;
        var ordered = includeCost
            ? rows.OrderByDescending(row => row.Value).ThenBy(row => row.SkuCode).ThenBy(row => row.BranchId)
            : rows.OrderByDescending(row => row.OnHand).ThenBy(row => row.SkuCode).ThenBy(row => row.BranchId);
        var items = await ordered
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(row => new InventoryValueRow(
                row.BranchId, row.SkuId, row.SkuCode, row.Name, row.OnHand,
                includeCost ? row.AvgCost : null, includeCost ? row.Value : null))
            .ToListAsync(ct);

        return new InventoryValueReport(
            items, query.Page, query.PageSize, await rows.CountAsync(ct),
            includeCost ? await rows.SumAsync(row => row.Value, ct) : null);
    }

    public async Task<IReadOnlyList<TopSellerRow>> ListTopSellersAsync(SalesWindowFilter filter, CancellationToken ct)
    {
        var since = filter.Since.ToUniversalTime();
        var top = await db.SalesFacts.AsNoTracking()
            .Where(fact => fact.ConfirmedAt >= since && (filter.BranchId == null || fact.BranchId == filter.BranchId))
            .GroupBy(fact => fact.SkuId)
            .Select(group => new { SkuId = group.Key, Quantity = group.Sum(fact => fact.Quantity) })
            .OrderByDescending(row => row.Quantity).ThenBy(row => row.SkuId)
            .Take(filter.Limit)
            .ToListAsync(ct);

        var skus = await FindSkuNamesAsync(top.Select(row => row.SkuId).ToList(), ct);
        return top.Select(row =>
        {
            var sku = skus.GetValueOrDefault(row.SkuId);
            return new TopSellerRow(row.SkuId, sku?.SkuCode, sku?.Name, row.Quantity);
        }).ToList();
    }

    public async Task<IReadOnlyList<SlowMoverRow>> ListSlowMoversAsync(SalesWindowFilter filter, CancellationToken ct)
    {
        // SKU còn tồn thực tế, cộng mọi chi nhánh khi không lọc chi nhánh.
        var stock = await db.StockSnapshots.AsNoTracking()
            .Where(snapshot => filter.BranchId == null || snapshot.BranchId == filter.BranchId)
            .GroupBy(snapshot => snapshot.SkuId)
            .Select(group => new { SkuId = group.Key, OnHand = group.Sum(snapshot => snapshot.OnHand) })
            .Where(row => row.OnHand > 0)
            .ToListAsync(ct);

        var skuIds = stock.Select(row => row.SkuId).ToList();
        var since = filter.Since.ToUniversalTime();
        var sold = await db.SalesFacts.AsNoTracking()
            .Where(fact => fact.ConfirmedAt >= since && skuIds.Contains(fact.SkuId)
                && (filter.BranchId == null || fact.BranchId == filter.BranchId))
            .GroupBy(fact => fact.SkuId)
            .Select(group => new { SkuId = group.Key, Quantity = group.Sum(fact => fact.Quantity) })
            .ToDictionaryAsync(row => row.SkuId, row => row.Quantity, ct);

        // SKU không bán được gì có số lượng 0 nên đứng đầu; cùng số lượng thì SKU tồn nhiều hơn đứng trước.
        var slowest = stock
            .Select(row => (row.SkuId, Quantity: sold.GetValueOrDefault(row.SkuId), row.OnHand))
            .OrderBy(row => row.Quantity).ThenByDescending(row => row.OnHand).ThenBy(row => row.SkuId)
            .Take(filter.Limit)
            .ToList();

        var skus = await FindSkuNamesAsync(slowest.Select(row => row.SkuId).ToList(), ct);
        return slowest.Select(row =>
        {
            var sku = skus.GetValueOrDefault(row.SkuId);
            return new SlowMoverRow(row.SkuId, sku?.SkuCode, sku?.Name, row.Quantity, row.OnHand);
        }).ToList();
    }

    private Task<Dictionary<Guid, SkuName>> FindSkuNamesAsync(List<Guid> skuIds, CancellationToken ct) =>
        db.SkuNames.AsNoTracking().Where(sku => skuIds.Contains(sku.SkuId)).ToDictionaryAsync(sku => sku.SkuId, ct);

    private static IQueryable<SaleLine> Where(IQueryable<SaleLine> lines, GrossProfitFilter filter) =>
        lines.Where(line => (filter.BranchId == null || line.BranchId == filter.BranchId)
            && (filter.Channel == null || line.Channel == filter.Channel)
            && (filter.SkuId == null || line.SkuId == filter.SkuId));

    // Cộng trong database theo đúng chiều nhóm; GroupBy None cộng tất cả thành một nhóm.
    private static Task<List<Bucket>> SumAsync(IQueryable<SaleLine> lines, GrossProfitGroupBy groupBy, CancellationToken ct) =>
        (groupBy switch
        {
            GrossProfitGroupBy.Day => lines.GroupBy(line => line.Date).Select(group => new Bucket
            {
                Date = group.Key,
                Quantity = group.Sum(line => line.Quantity),
                Revenue = group.Sum(line => line.Revenue),
                Cogs = group.Sum(line => line.Cogs),
            }),
            GrossProfitGroupBy.Branch => lines.GroupBy(line => line.BranchId).Select(group => new Bucket
            {
                BranchId = group.Key,
                Quantity = group.Sum(line => line.Quantity),
                Revenue = group.Sum(line => line.Revenue),
                Cogs = group.Sum(line => line.Cogs),
            }),
            GrossProfitGroupBy.Channel => lines.GroupBy(line => line.Channel).Select(group => new Bucket
            {
                Channel = group.Key,
                Quantity = group.Sum(line => line.Quantity),
                Revenue = group.Sum(line => line.Revenue),
                Cogs = group.Sum(line => line.Cogs),
            }),
            GrossProfitGroupBy.Sku => lines.GroupBy(line => line.SkuId).Select(group => new Bucket
            {
                SkuId = group.Key,
                Quantity = group.Sum(line => line.Quantity),
                Revenue = group.Sum(line => line.Revenue),
                Cogs = group.Sum(line => line.Cogs),
            }),
            _ => lines.GroupBy(_ => 1).Select(group => new Bucket
            {
                Quantity = group.Sum(line => line.Quantity),
                Revenue = group.Sum(line => line.Revenue),
                Cogs = group.Sum(line => line.Cogs),
            }),
        }).ToListAsync(ct);

    // Một dòng bán ở mức ngày, chung cho sales_facts và daily_aggregates.
    private sealed class SaleLine
    {
        public DateOnly Date { get; init; }

        public Guid BranchId { get; init; }

        public string Channel { get; init; } = null!;

        public Guid SkuId { get; init; }

        public int Quantity { get; init; }

        public decimal Revenue { get; init; }

        public decimal Cogs { get; init; }
    }

    // Tổng của một nhóm; chỉ trường của chiều đang nhóm có giá trị.
    private sealed class Bucket
    {
        public DateOnly? Date { get; init; }

        public Guid? BranchId { get; init; }

        public string? Channel { get; init; }

        public Guid? SkuId { get; init; }

        public int Quantity { get; init; }

        public decimal Revenue { get; init; }

        public decimal Cogs { get; init; }
    }
}
