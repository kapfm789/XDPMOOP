using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Tenancy;
using Oism.Identity.Application;
using Oism.Identity.Application.Users;
using Oism.Identity.Domain;

namespace Oism.Identity.Infrastructure.Repositories;

internal sealed class UserRepository(IdentityDbContext db, ITenantContext tenantContext) : IUserRepository
{
    // Một trong các chỗ được phép bỏ filter: docs/architecture/multi-tenancy.md.
    public async Task<User?> FindByIdentifierAsync(string identifier, CancellationToken ct)
    {
        var email = User.NormalizeEmail(identifier);
        var phone = User.NormalizePhone(identifier);
        var user = await db.Users.IgnoreQueryFilters()
            .SingleOrDefaultAsync(candidate => candidate.Email == email || candidate.Phone == phone, ct);

        if (user is not null)
            tenantContext.Set(user.TenantId);
        return user;
    }

    public Task<User?> GetAsync(Guid id, CancellationToken ct) =>
        db.Users.SingleOrDefaultAsync(user => user.Id == id, ct);

    public async Task<PagedResult<UserDto>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        var users = db.Users.AsNoTracking();
        var items = await users
            .OrderByDescending(user => user.CreatedAt).ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(user => new UserDto(
                user.Id, user.FullName, user.Email, user.Phone, user.Role.ToString(), user.BranchId, user.IsActive, user.CreatedAt))
            .ToListAsync(ct);
        return new PagedResult<UserDto>(items, page, pageSize, await users.CountAsync(ct));
    }

    public void Add(User user) => db.Add(user);
}
