using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Messaging;
using Oism.BuildingBlocks.Persistence;
using Oism.BuildingBlocks.Tenancy;
using Oism.Catalog.Domain;

namespace Oism.Catalog.Infrastructure;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options, ITenantContext tenant)
    : OismDbContext(options, tenant, Schema), IHasOutbox
{
    public const string Schema = "catalog";

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Brand> Brands => Set<Brand>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Sku> Skus => Set<Sku>();

    public DbSet<Barcode> Barcodes => Set<Barcode>();
}
