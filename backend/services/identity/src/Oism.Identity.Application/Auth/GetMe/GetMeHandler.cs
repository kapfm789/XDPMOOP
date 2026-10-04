using Oism.Identity.Application.Tenants;
using Oism.Identity.Application.Users;
using Oism.SharedKernel;

namespace Oism.Identity.Application.Auth.GetMe;

public sealed record MeDto(Guid Id, string FullName, string Role, Guid? BranchId, string TenantName);

public sealed class GetMeHandler(IUserRepository users, ITenantRepository tenants)
{
    public async Task<MeDto> Handle(Guid userId, CancellationToken ct)
    {
        var user = await users.GetAsync(userId, ct) ?? throw new NotFoundException("người dùng");
        var tenantName = await tenants.GetNameAsync(user.TenantId, ct) ?? throw new NotFoundException("tenant");
        return new MeDto(user.Id, user.FullName, user.Role.ToString(), user.BranchId, tenantName);
    }
}
