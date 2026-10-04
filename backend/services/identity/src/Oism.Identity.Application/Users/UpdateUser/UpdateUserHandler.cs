using FluentValidation;
using FluentValidation.Results;
using Oism.Identity.Domain;
using Oism.SharedKernel;

namespace Oism.Identity.Application.Users.UpdateUser;

public sealed class UpdateUserHandler(IUnitOfWork unitOfWork, IUserRepository users)
{
    private static readonly UpdateUserValidator Validator = new();

    public async Task<UserDto> Handle(UpdateUserCommand command, CancellationToken ct)
    {
        Validator.ValidateAndThrow(command);

        await using var transaction = await unitOfWork.BeginAsync(ct);

        var user = await users.GetAsync(command.Id, ct) ?? throw new NotFoundException("người dùng");

        // API này chỉ gán được Staff hoặc Cashier, nên sửa Owner qua đây là hạ vai trò của Owner duy nhất.
        if (user.Role == UserRole.Owner)
            throw new ValidationException([new ValidationFailure(nameof(command.Id), "Không sửa được tài khoản Owner qua API này")]);

        user.Update(command.FullName, Enum.Parse<UserRole>(command.Role), command.BranchId, command.IsActive);

        await transaction.CommitAsync(ct);
        return UserDto.From(user);
    }
}
