using Oism.BuildingBlocks.Messaging;
using Oism.Contracts;
using Oism.Insights.Application.Projections.RecordSale;

namespace Oism.Insights.Api.Consumers;

public sealed class OrderConfirmedConsumer(RecordSaleHandler recordSale) : EventConsumer<OrderConfirmed>
{
    public override Task HandleAsync(OrderConfirmed payload, CancellationToken ct) => recordSale.Handle(payload, ct);
}
