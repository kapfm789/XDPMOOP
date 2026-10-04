using FluentValidation;
using Oism.Identity.Domain;

namespace Oism.Identity.Application.Users.CreateUser;

// UC-AUTH-03: Owner tạo tài khoản Staff hoặc Cashier trong tenant của mình.
public sealed class CreateUserHandler(IUnitOfWork unitOfWork, IUserRepository users, IPasswordHasher passwords, IClock clock)
{
    private static readonly CreateUserValidator Validator = new();

    public async Task<UserDto> Handle(CreateUserCommand command, CancellationToken ct)
    {
        Validator.ValidateAndThrow(command);
        var passwordHash = passwords.Hash(command.Password);

        await using var transaction = await unitOfWork.BeginAsync(ct);

        // Kiểm chi nhánh có thật trong tenant được thêm ở W1-05, khi có bảng branches.
        var user = User.Create(
            command.FullName, command.Email, command.Phone, passwordHash,
            Enum.Parse<UserRole>(command.Role), command.BranchId, clock.UtcNow);
        users.Add(user);

        // Email hoặc số điện thoại đã có trong hệ thống: chỉ mục unique từ chối, CommitAsync ném DuplicateException.
        await transaction.CommitAsync(ct);
        return UserDto.From(user);
    }
}
