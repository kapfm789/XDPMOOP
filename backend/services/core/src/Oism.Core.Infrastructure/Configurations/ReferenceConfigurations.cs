using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oism.Core.Domain.References;

namespace Oism.Core.Infrastructure.Configurations;

// Bảng và ràng buộc: docs/design/data-model/core.md mục "Chứng từ kho và bảng phụ".
internal sealed class SkuRefConfiguration : IEntityTypeConfiguration<SkuRef>
{
    public void Configure(EntityTypeBuilder<SkuRef> builder)
    {
        builder.HasKey(sku => new { sku.TenantId, sku.SkuId });
        builder.Property(sku => sku.RetailPrice).HasPrecision(18, 4);
        builder.Property(sku => sku.WholesalePrice).HasPrecision(18, 4);
        builder.HasIndex(sku => new { sku.TenantId, sku.SkuCode }).IsUnique();
    }
}

internal sealed class BranchRefConfiguration : IEntityTypeConfiguration<BranchRef>
{
    public void Configure(EntityTypeBuilder<BranchRef> builder) =>
        builder.HasKey(branch => new { branch.TenantId, branch.BranchId });
}
