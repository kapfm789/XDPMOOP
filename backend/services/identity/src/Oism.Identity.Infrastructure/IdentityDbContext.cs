using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Persistence;
using Oism.BuildingBlocks.Tenancy;

namespace Oism.Identity.Infrastructure;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenant)
    : OismDbContext(options, tenant, Schema)
{
    public const string Schema = "identity";
}
