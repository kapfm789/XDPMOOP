namespace Oism.Identity.Application.Branches.ListBranches;

public sealed class ListBranchesHandler(IBranchRepository branches)
{
    public Task<IReadOnlyList<BranchDto>> Handle(bool? isActive, CancellationToken ct) => branches.ListAsync(isActive, ct);
}
