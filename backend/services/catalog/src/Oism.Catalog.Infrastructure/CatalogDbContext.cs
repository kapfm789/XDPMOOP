using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Persistence;
using Oism.BuildingBlocks.Tenancy;

namespace Oism.Catalog.Infrastructure;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options, ITenantContext tenant)
    : OismDbContext(options, tenant, Schema)
{
    public const string Schema = "catalog";
}
