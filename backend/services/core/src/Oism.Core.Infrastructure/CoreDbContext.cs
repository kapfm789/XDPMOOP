using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Messaging;
using Oism.BuildingBlocks.Persistence;
using Oism.BuildingBlocks.Tenancy;
using Oism.Core.Domain.Inventory;
using Oism.Core.Domain.Orders;
using Oism.Core.Domain.References;

namespace Oism.Core.Infrastructure;

public sealed class CoreDbContext(DbContextOptions<CoreDbContext> options, ITenantContext tenant)
    : OismDbContext(options, tenant, Schema), IHasInbox
{
    public const string Schema = "core";

    public DbSet<SkuRef> SkuRefs => Set<SkuRef>();

    public DbSet<BranchRef> BranchRefs => Set<BranchRef>();

    public DbSet<InventoryBalance> InventoryBalances => Set<InventoryBalance>();

    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
}
