using FluentValidation;
using Oism.Identity.Application.Users;
using Oism.Identity.Domain;

namespace Oism.Identity.Application.Tenants.RegisterTenant;

// UC-AUTH-01: tạo tenant kèm người dùng Owner đầu tiên.
public sealed class RegisterTenantHandler(
    IUnitOfWork unitOfWork, ITenantRepository tenants, IUserRepository users, IPasswordHasher passwords, IClock clock)
{
    private static readonly RegisterTenantValidator Validator = new();

    public async Task<RegisterTenantResult> Handle(RegisterTenantCommand command, CancellationToken ct)
    {
        Validator.ValidateAndThrow(command);
        var passwordHash = passwords.Hash(command.Password);

        await using var transaction = await unitOfWork.BeginAsync(ct);

        var tenant = new Tenant(command.TenantName.Trim(), clock.UtcNow);
        tenants.Add(tenant);
        var owner = User.Create(
            command.OwnerName, command.Email, command.Phone, passwordHash, UserRole.Owner, branchId: null, clock.UtcNow);
        users.Add(owner);

        // Email hoặc số điện thoại đã có trong hệ thống: chỉ mục unique từ chối, CommitAsync ném DuplicateException
        // và cả tenant lẫn người dùng đều không được tạo.
        await transaction.CommitAsync(ct);
        return new RegisterTenantResult(tenant.Id, owner.Id);
    }
}
