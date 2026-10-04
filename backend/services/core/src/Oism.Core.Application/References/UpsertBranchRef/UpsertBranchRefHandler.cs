using Oism.Contracts;
using Oism.Core.Domain.References;

namespace Oism.Core.Application.References.UpsertBranchRef;

// Ghi đè bản sao chi nhánh theo BranchUpserted. Không mở transaction: phần chung của consumer đã mở,
// ghi inbox và sẽ commit (docs/architecture/messaging.md mục "Dùng trong một service").
public sealed class UpsertBranchRefHandler(IReferenceRepository references)
{
    public async Task Handle(BranchUpserted message, CancellationToken ct)
    {
        var branch = await references.FindBranchAsync(message.BranchId, ct);
        if (branch is null)
            references.Add(branch = new BranchRef(message.BranchId));

        branch.Apply(message.Code, message.Name, message.Type, message.IsActive, message.Version);
    }
}
