using Oism.BuildingBlocks.Messaging;
using Oism.Contracts;

namespace Oism.Insights.Api.Consumers;

// Đơn bị từ chối chưa từng có trong `core` nên không tác động số liệu doanh thu. Dòng inbox mà consumer base ghi
// là bản ghi nhận duy nhất (docs/design/api/insights.md).
public sealed class OrderRejectedConsumer : EventConsumer<OrderRejected>
{
    public override Task HandleAsync(OrderRejected payload, CancellationToken ct) => Task.CompletedTask;
}
