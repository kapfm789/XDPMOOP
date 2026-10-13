using Oism.BuildingBlocks.Messaging;
using Oism.Channel.Application.Webhooks.ApplyOrderResult;
using Oism.Contracts;

namespace Oism.Channel.Api.Consumers;

// `core` giữ được hàng: bản ghi webhook sang Reserved kèm order_id (docs/design/api/channel.md).
public sealed class OrderReservedConsumer(ApplyOrderResultHandler applyOrderResult) : EventConsumer<OrderReserved>
{
    public override Task HandleAsync(OrderReserved payload, CancellationToken ct) => applyOrderResult.Handle(payload, ct);
}
