using Oism.BuildingBlocks.Messaging;
using Oism.Contracts;

namespace Oism.Insights.Api.Consumers;

// Đơn bị hủy không tác động số liệu doanh thu: chỉ đơn đã Confirmed mới vào sales_facts và đơn Confirmed không hủy được
// (ADR-0008). Dòng inbox mà consumer base ghi là bản ghi nhận duy nhất (docs/design/api/insights.md).
public sealed class OrderCancelledConsumer : EventConsumer<OrderCancelled>
{
    public override Task HandleAsync(OrderCancelled payload, CancellationToken ct) => Task.CompletedTask;
}
