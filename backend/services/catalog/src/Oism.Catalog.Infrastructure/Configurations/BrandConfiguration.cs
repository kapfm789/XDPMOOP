using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oism.Catalog.Domain;

namespace Oism.Catalog.Infrastructure.Configurations;

internal sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder) =>
        builder.HasIndex(brand => new { brand.TenantId, brand.Name }).IsUnique();
}
