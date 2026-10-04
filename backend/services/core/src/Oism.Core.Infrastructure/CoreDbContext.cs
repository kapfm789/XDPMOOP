using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Messaging;
using Oism.BuildingBlocks.Persistence;
using Oism.BuildingBlocks.Tenancy;
using Oism.Core.Domain.Inventory;
using Oism.Core.Domain.Orders;
using Oism.Core.Domain.References;

namespace Oism.Core.Infrastructure;

public sealed class CoreDbContext(DbContextOptions<CoreDbContext> options, ITenantContext tenant)
    : OismDbContext(options, tenant, Schema), IHasOutbox, IHasInbox
{
    public const string Schema = "core";

    public DbSet<SkuRef> SkuRefs => Set<SkuRef>();

    public DbSet<BranchRef> BranchRefs => Set<BranchRef>();

    public DbSet<InventoryBalance> InventoryBalances => Set<InventoryBalance>();

    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<PurchaseReceipt> PurchaseReceipts => Set<PurchaseReceipt>();

    public DbSet<PurchaseReceiptItem> PurchaseReceiptItems => Set<PurchaseReceiptItem>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<Reservation> Reservations => Set<Reservation>();
}
