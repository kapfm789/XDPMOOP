using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oism.Insights.Domain.References;
using Oism.Insights.Domain.Sales;
using Oism.Insights.Domain.Stock;

namespace Oism.Insights.Infrastructure.Configurations;

// Bảng và chỉ mục: docs/design/data-model/insights.md.
internal sealed class SalesFactConfiguration : IEntityTypeConfiguration<SalesFact>
{
    public void Configure(EntityTypeBuilder<SalesFact> builder)
    {
        builder.HasKey(fact => fact.OrderItemId);
        builder.Property(fact => fact.OrderItemId).ValueGeneratedNever();
        builder.Property(fact => fact.Revenue).HasPrecision(18, 4);
        builder.Property(fact => fact.Cogs).HasPrecision(18, 4);
        builder.HasIndex(fact => new { fact.TenantId, fact.ConfirmedAt });
        builder.HasIndex(fact => new { fact.TenantId, fact.SkuId, fact.ConfirmedAt });
        builder.HasIndex(fact => new { fact.TenantId, fact.BranchId, fact.ConfirmedAt });
    }
}

internal sealed class DailyAggregateConfiguration : IEntityTypeConfiguration<DailyAggregate>
{
    public void Configure(EntityTypeBuilder<DailyAggregate> builder)
    {
        builder.HasKey(aggregate => new { aggregate.TenantId, aggregate.Date, aggregate.BranchId, aggregate.Channel, aggregate.SkuId });
        builder.Property(aggregate => aggregate.Revenue).HasPrecision(18, 4);
        builder.Property(aggregate => aggregate.Cogs).HasPrecision(18, 4);
    }
}

internal sealed class StockSnapshotConfiguration : IEntityTypeConfiguration<StockSnapshot>
{
    public void Configure(EntityTypeBuilder<StockSnapshot> builder)
    {
        builder.HasKey(snapshot => new { snapshot.TenantId, snapshot.BranchId, snapshot.SkuId });
        builder.Property(snapshot => snapshot.AvgCost).HasPrecision(18, 4);
    }
}

internal sealed class SkuNameConfiguration : IEntityTypeConfiguration<SkuName>
{
    public void Configure(EntityTypeBuilder<SkuName> builder) =>
        builder.HasKey(sku => new { sku.TenantId, sku.SkuId });
}
