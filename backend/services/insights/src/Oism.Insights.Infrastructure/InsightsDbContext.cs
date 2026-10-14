using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Messaging;
using Oism.BuildingBlocks.Persistence;
using Oism.BuildingBlocks.Tenancy;
using Oism.Insights.Domain.References;
using Oism.Insights.Domain.Sales;
using Oism.Insights.Domain.Stock;

namespace Oism.Insights.Infrastructure;

// `insights` chỉ nhận event, không phát: có inbox, không có outbox (docs/design/data-model/insights.md).
public sealed class InsightsDbContext(DbContextOptions<InsightsDbContext> options, ITenantContext tenant)
    : OismDbContext(options, tenant, Schema), IHasInbox
{
    public const string Schema = "insights";

    public DbSet<SalesFact> SalesFacts => Set<SalesFact>();

    public DbSet<DailyAggregate> DailyAggregates => Set<DailyAggregate>();

    public DbSet<StockSnapshot> StockSnapshots => Set<StockSnapshot>();

    public DbSet<SkuName> SkuNames => Set<SkuName>();
}
