using Oism.BuildingBlocks.Messaging;
using Oism.Contracts;
using Oism.Insights.Application.Notifications.NotifyOrderCreated;

namespace Oism.Insights.Api.Consumers;

public sealed class OrderReservedConsumer(NotifyOrderCreatedHandler notifyOrderCreated) : EventConsumer<OrderReserved>
{
    public override Task HandleAsync(OrderReserved payload, CancellationToken ct) => notifyOrderCreated.Handle(payload, ct);
}
