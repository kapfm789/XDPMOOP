using Oism.BuildingBlocks.Messaging;
using Oism.Contracts;
using Oism.Insights.Application.Projections.ApplyStockChange;

namespace Oism.Insights.Api.Consumers;

public sealed class StockChangedConsumer(ApplyStockChangeHandler applyStockChange) : EventConsumer<StockChanged>
{
    public override Task HandleAsync(StockChanged payload, CancellationToken ct) => applyStockChange.Handle(payload, ct);
}
