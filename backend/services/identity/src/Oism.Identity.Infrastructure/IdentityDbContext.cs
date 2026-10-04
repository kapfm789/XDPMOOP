using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Persistence;
using Oism.BuildingBlocks.Tenancy;
using Oism.Identity.Domain;

namespace Oism.Identity.Infrastructure;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenant)
    : OismDbContext(options, tenant, Schema)
{
    public const string Schema = "identity";

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
}
