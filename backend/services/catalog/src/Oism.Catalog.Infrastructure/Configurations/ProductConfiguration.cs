using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oism.Catalog.Domain;

namespace Oism.Catalog.Infrastructure.Configurations;

// Bảng và ràng buộc: docs/design/data-model/catalog.md.
internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasOne<Category>().WithMany().HasForeignKey(product => product.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Brand>().WithMany().HasForeignKey(product => product.BrandId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(product => product.Skus).WithOne().HasForeignKey(sku => sku.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SkuConfiguration : IEntityTypeConfiguration<Sku>
{
    public void Configure(EntityTypeBuilder<Sku> builder)
    {
        // SKU mới được thêm vào sản phẩm đang theo dõi chứ không qua DbContext.Add. Khóa do Domain đặt sẵn,
        // nên phải khai báo là không tự sinh; nếu không EF Core coi SKU có khóa là đã tồn tại và chạy UPDATE.
        builder.Property(sku => sku.Id).ValueGeneratedNever();
        builder.ToTable(table => table.HasCheckConstraint(
            "ck_skus_prices_not_negative", "retail_price >= 0 AND wholesale_price >= 0"));
        builder.Property(sku => sku.RetailPrice).HasPrecision(18, 4);
        builder.Property(sku => sku.WholesalePrice).HasPrecision(18, 4);

        // Lưu dạng chuỗi JSON vào cột jsonb. Thuộc tính luôn được thay cả bộ, không sửa tại chỗ.
        builder.Property(sku => sku.Attributes)
            .HasColumnType("jsonb")
            .HasConversion(
                attributes => JsonSerializer.Serialize(attributes, JsonSerializerOptions.Default),
                json => JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonSerializerOptions.Default)!,
                new ValueComparer<IReadOnlyDictionary<string, string>>(
                    (left, right) => left!.Count == right!.Count && !left.Except(right).Any(),
                    attributes => attributes.Count,
                    attributes => attributes));

        builder.HasMany(sku => sku.Barcodes).WithOne().HasForeignKey(barcode => barcode.SkuId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(sku => new { sku.TenantId, sku.SkuCode }).IsUnique();
    }
}

internal sealed class BarcodeConfiguration : IEntityTypeConfiguration<Barcode>
{
    public void Configure(EntityTypeBuilder<Barcode> builder)
    {
        // Như SKU: mã vạch mới được thêm vào SKU đang theo dõi.
        builder.Property(barcode => barcode.Id).ValueGeneratedNever();
        builder.Property(barcode => barcode.Symbology).HasConversion<string>();
        builder.HasIndex(barcode => new { barcode.TenantId, barcode.Code }).IsUnique();
    }
}
