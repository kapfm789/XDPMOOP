using Oism.Contracts;
using Oism.Insights.Application.Reports;
using Oism.Insights.Domain.Sales;

namespace Oism.Insights.Application.Projections.RecordSale;

// Mỗi dòng của OrderConfirmed thành một dòng sales_facts (docs/design/api/insights.md mục "Đường vào không phải HTTP").
// Không mở transaction: consumer base đã mở, ghi inbox và sẽ commit (docs/architecture/messaging.md).
// Inbox chặn cùng một eventId tới lại; dòng đơn đã ghi thì bỏ qua, nên một bản phát lại mang eventId khác
// cũng không cộng đôi (UC-REP-01 AC-5, T22).
public sealed class RecordSaleHandler(IProjectionRepository projections, IDailyAggregateRepository aggregates)
{
    public async Task Handle(OrderConfirmed message, CancellationToken ct)
    {
        var recorded = await projections.ListRecordedOrderItemsAsync(
            message.Lines.Select(line => line.OrderItemId).ToList(), ct);
        var newLines = message.Lines.Where(line => !recorded.Contains(line.OrderItemId)).ToList();
        if (newLines.Count == 0)
            return;

        foreach (var line in newLines)
        {
            projections.Add(SalesFact.Record(
                line.OrderItemId, message.OrderId, message.BranchId, line.SkuId, message.Channel, line.Quantity, line.UnitPrice,
                line.Discount, line.CostPrice, message.ConfirmedAt));
        }

        // Báo cáo đọc daily_aggregates cho ngày đã tổng hợp. Đơn tới trễ sau khi ngày của nó đã được tổng hợp thì bỏ
        // bản tổng hợp của ngày đó, để báo cáo đọc lại sales_facts cho tới lần dựng lại sau.
        await aggregates.RemoveAsync(ReportCalendar.DateOf(message.ConfirmedAt), ct);
    }
}
