using Oism.BuildingBlocks.Messaging;
using Oism.Contracts;
using Oism.Insights.Application.Projections.UpsertSkuName;

namespace Oism.Insights.Api.Consumers;

public sealed class SkuUpsertedConsumer(UpsertSkuNameHandler upsertSkuName) : EventConsumer<SkuUpserted>
{
    public override Task HandleAsync(SkuUpserted payload, CancellationToken ct) => upsertSkuName.Handle(payload, ct);
}
