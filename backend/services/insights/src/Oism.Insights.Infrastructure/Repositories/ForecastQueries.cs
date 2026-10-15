using Microsoft.EntityFrameworkCore;
using Oism.Insights.Application.Forecasting;
using Oism.Insights.Application.Forecasting.GetReorderSuggestions;
using Oism.Insights.Domain.Forecasting;

namespace Oism.Insights.Infrastructure.Repositories;

// Chỉ đọc. Mã, tên SKU lấy từ sku_names và tồn khả dụng hiện tại lấy từ stock_snapshots của chính schema insights.
internal sealed class ForecastQueries(InsightsDbContext db) : IForecastQueries
{
    public async Task<ForecastRunDto?> FindLatestRunAsync(bool succeededOnly, CancellationToken ct)
    {
        var run = await db.ForecastRuns.AsNoTracking()
            .Where(run => !succeededOnly || run.Status == ForecastRunStatus.Succeeded)
            .OrderByDescending(run => run.RanAt).ThenByDescending(run => run.Id)
            .FirstOrDefaultAsync(ct);
        return run is null
            ? null
            : new ForecastRunDto(run.Id, run.RanAt, run.Method, run.Status.ToString(), run.Error, run.ForecastWape, run.BaselineWape);
    }

    public async Task<(IReadOnlyList<ReorderSuggestionRow> Items, int Total)> ListResultsAsync(
        Guid runId, GetReorderSuggestionsQuery query, CancellationToken ct)
    {
        var rows =
            from result in db.ForecastResults.AsNoTracking()
            where result.RunId == runId && (query.BranchId == null || result.BranchId == query.BranchId)
            join sku in db.SkuNames.AsNoTracking() on result.SkuId equals sku.SkuId into skus
            from sku in skus.DefaultIfEmpty()
            join snapshot in db.StockSnapshots.AsNoTracking()
                on new { result.BranchId, result.SkuId } equals new { snapshot.BranchId, snapshot.SkuId } into snapshots
            from snapshot in snapshots.DefaultIfEmpty()
            select new { result, sku.SkuCode, sku.Name, Available = (int?)snapshot.Available };

        var items = await rows
            .OrderByDescending(row => row.result.HasEnoughData)
            .ThenByDescending(row => row.result.SuggestedReorderQty)
            .ThenByDescending(row => row.result.ForecastQty)
            .ThenBy(row => row.SkuCode).ThenBy(row => row.result.BranchId)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(row => new ReorderSuggestionRow(
                row.result.BranchId, row.result.SkuId, row.SkuCode, row.Name, row.result.HasEnoughData, row.result.ForecastQty,
                row.Available ?? 0, row.result.SuggestedReorderQty))
            .ToListAsync(ct);
        return (items, await rows.CountAsync(ct));
    }
}
