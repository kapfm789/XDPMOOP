using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Persistence;
using Oism.BuildingBlocks.Tenancy;

namespace Oism.Core.Infrastructure;

public sealed class CoreDbContext(DbContextOptions<CoreDbContext> options, ITenantContext tenant)
    : OismDbContext(options, tenant, Schema)
{
    public const string Schema = "core";
}
