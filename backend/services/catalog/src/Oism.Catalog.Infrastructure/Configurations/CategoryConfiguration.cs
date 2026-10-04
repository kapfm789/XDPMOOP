using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oism.Catalog.Domain;

namespace Oism.Catalog.Infrastructure.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasOne<Category>().WithMany().HasForeignKey(category => category.ParentId).OnDelete(DeleteBehavior.Restrict);
        // Hai danh mục gốc (parent_id null) cùng tên cũng là trùng.
        builder.HasIndex(category => new { category.TenantId, category.ParentId, category.Name }).IsUnique().AreNullsDistinct(false);
    }
}
