using Oism.BuildingBlocks.Messaging;
using Oism.Contracts;
using Oism.Core.Application.References.UpsertSkuRef;

namespace Oism.Core.Api.Consumers;

public sealed class SkuUpsertedConsumer(UpsertSkuRefHandler upsertSkuRef) : EventConsumer<SkuUpserted>
{
    public override Task HandleAsync(SkuUpserted payload, CancellationToken ct) => upsertSkuRef.Handle(payload, ct);
}
