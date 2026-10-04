using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Tenancy;
using Oism.Identity.Application.Auth;
using Oism.Identity.Domain;

namespace Oism.Identity.Infrastructure.Repositories;

internal sealed class RefreshTokenRepository(IdentityDbContext db, ITenantContext tenantContext) : IRefreshTokenRepository
{
    // Một trong các chỗ được phép bỏ filter: docs/architecture/multi-tenancy.md.
    // Khóa dòng để hai lần làm mới cùng một token không cùng thành công.
    public async Task<RefreshToken?> FindForUpdateAsync(string tokenHash, CancellationToken ct)
    {
        var token = await db.RefreshTokens
            .FromSql($"SELECT * FROM identity.refresh_tokens WHERE token_hash = {tokenHash} FOR UPDATE")
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(ct);

        if (token is not null)
            tenantContext.Set(token.TenantId);
        return token;
    }

    public Task<RefreshToken?> GetAsync(string tokenHash, CancellationToken ct) =>
        db.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == tokenHash, ct);

    public Task RevokeAllAsync(Guid userId, DateTimeOffset now, CancellationToken ct) =>
        db.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAt, now), ct);

    public void Add(RefreshToken token) => db.Add(token);
}
