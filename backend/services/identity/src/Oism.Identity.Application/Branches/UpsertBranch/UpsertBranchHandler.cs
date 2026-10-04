using FluentValidation;
using Oism.Identity.Domain;
using Oism.SharedKernel;

namespace Oism.Identity.Application.Branches.UpsertBranch;

// UC-AUTH-04: Owner khai báo và sửa chi nhánh; mọi thay đổi phát BranchUpserted.
public sealed class UpsertBranchHandler(IUnitOfWork unitOfWork, IBranchRepository branches, IEventPublisher events)
{
    private static readonly UpsertBranchValidator Validator = new();

    public async Task<BranchDto> Handle(UpsertBranchCommand command, CancellationToken ct)
    {
        Validator.ValidateAndThrow(command);
        var type = Enum.Parse<BranchType>(command.Type);

        await using var transaction = await unitOfWork.BeginAsync(ct);

        Branch branch;
        if (command.Id is { } id)
        {
            branch = await branches.GetForUpdateAsync(id, ct) ?? throw new NotFoundException("chi nhánh");
            branch.Update(command.Name, type, command.Address);
        }
        else
        {
            branch = Branch.Create(command.Code!, command.Name, type, command.Address);
            branches.Add(branch);
        }

        events.Enqueue(BranchUpsertedFactory.From(branch));

        // Trùng mã trong tenant (UC-AUTH-04 AC-2): chỉ mục unique từ chối, CommitAsync ném DuplicateException.
        await transaction.CommitAsync(ct);
        return BranchDto.From(branch);
    }
}
