using Oism.BuildingBlocks.Messaging;
using Oism.Contracts;
using Oism.Core.Application.References.UpsertBranchRef;

namespace Oism.Core.Api.Consumers;

public sealed class BranchUpsertedConsumer(UpsertBranchRefHandler upsertBranchRef) : EventConsumer<BranchUpserted>
{
    public override Task HandleAsync(BranchUpserted payload, CancellationToken ct) => upsertBranchRef.Handle(payload, ct);
}
