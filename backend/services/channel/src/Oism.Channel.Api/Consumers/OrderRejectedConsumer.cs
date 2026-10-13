using Oism.BuildingBlocks.Messaging;
using Oism.Channel.Application.Webhooks.ApplyOrderResult;
using Oism.Contracts;

namespace Oism.Channel.Api.Consumers;

// `core` từ chối đơn: bản ghi webhook sang Rejected kèm lý do (docs/design/api/channel.md).
public sealed class OrderRejectedConsumer(ApplyOrderResultHandler applyOrderResult) : EventConsumer<OrderRejected>
{
    public override Task HandleAsync(OrderRejected payload, CancellationToken ct) => applyOrderResult.Handle(payload, ct);
}
