using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Messaging;
using Oism.BuildingBlocks.Persistence;
using Oism.BuildingBlocks.Tenancy;
using Oism.Core.Domain.References;

namespace Oism.Core.Infrastructure;

public sealed class CoreDbContext(DbContextOptions<CoreDbContext> options, ITenantContext tenant)
    : OismDbContext(options, tenant, Schema), IHasInbox
{
    public const string Schema = "core";

    public DbSet<SkuRef> SkuRefs => Set<SkuRef>();

    public DbSet<BranchRef> BranchRefs => Set<BranchRef>();
}
