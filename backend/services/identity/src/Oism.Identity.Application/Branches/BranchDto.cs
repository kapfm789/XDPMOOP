using Oism.Contracts;
using Oism.Identity.Domain;

namespace Oism.Identity.Application.Branches;

public sealed record BranchDto(Guid Id, string Code, string Name, string Type, string? Address, bool IsActive)
{
    public static BranchDto From(Branch branch) =>
        new(branch.Id, branch.Code, branch.Name, branch.Type.ToString(), branch.Address, branch.IsActive);
}

internal static class BranchUpsertedFactory
{
    // Thông điệp trạng thái: mang toàn bộ trạng thái hiện tại của chi nhánh (docs/design/events.md).
    public static BranchUpserted From(Branch branch) =>
        new(branch.Id, branch.Code, branch.Name, branch.Type.ToString(), branch.IsActive, branch.Version);
}
