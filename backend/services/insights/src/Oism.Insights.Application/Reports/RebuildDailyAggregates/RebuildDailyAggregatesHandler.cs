using Oism.Insights.Domain.Sales;

namespace Oism.Insights.Application.Reports.RebuildDailyAggregates;

public sealed record RebuildDailyAggregatesCommand(DateOnly Date);

// UC-REP-01 AC-8: dựng lại daily_aggregates của một ngày cho tenant hiện tại từ sales_facts. Xóa rồi ghi lại trong
// một transaction, nên chạy lại cho cùng ngày cho ra cùng kết quả và không cộng dồn.
public sealed class RebuildDailyAggregatesHandler(IUnitOfWork unitOfWork, IDailyAggregateRepository aggregates)
{
    public async Task Handle(RebuildDailyAggregatesCommand command, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginAsync(ct);

        var totals = await aggregates.SumSalesAsync(command.Date, ct);
        await aggregates.RemoveAsync(command.Date, ct);
        foreach (var total in totals)
        {
            aggregates.Add(DailyAggregate.Of(
                command.Date, total.BranchId, total.Channel, total.SkuId, total.Quantity, total.Revenue, total.Cogs));
        }

        await transaction.CommitAsync(ct);
    }
}
