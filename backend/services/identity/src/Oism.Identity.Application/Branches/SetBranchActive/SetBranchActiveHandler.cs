using Oism.SharedKernel;

namespace Oism.Identity.Application.Branches.SetBranchActive;

public sealed record SetBranchActiveCommand(Guid Id, bool IsActive);

// UC-AUTH-04 AC-3: tắt chi nhánh. `core` nhận trạng thái mới qua BranchUpserted và tự chặn chứng từ mới.
public sealed class SetBranchActiveHandler(IUnitOfWork unitOfWork, IBranchRepository branches, IEventPublisher events)
{
    public async Task<BranchDto> Handle(SetBranchActiveCommand command, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginAsync(ct);

        var branch = await branches.GetForUpdateAsync(command.Id, ct) ?? throw new NotFoundException("chi nhánh");
        branch.SetActive(command.IsActive);
        events.Enqueue(BranchUpsertedFactory.From(branch));

        await transaction.CommitAsync(ct);
        return BranchDto.From(branch);
    }
}
