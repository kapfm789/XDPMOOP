using Oism.SharedKernel;

namespace Oism.Identity.Application.Branches.GetBranch;

// UC-AUTH-04 AC-6: POS đọc chi nhánh đang bán để lấy tài khoản nhận chuyển khoản.
public sealed class GetBranchHandler(IBranchRepository branches)
{
    public async Task<BranchDto> Handle(Guid id, CancellationToken ct) =>
        await branches.GetAsync(id, ct) ?? throw new NotFoundException("chi nhánh");
}
