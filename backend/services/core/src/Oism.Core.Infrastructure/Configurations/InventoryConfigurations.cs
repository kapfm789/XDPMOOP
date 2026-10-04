using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oism.Core.Domain.Inventory;

namespace Oism.Core.Infrastructure.Configurations;

// Bảng và ràng buộc: docs/design/data-model/core.md mục "inventory_balances" và "inventory_transactions".
internal sealed class InventoryBalanceConfiguration : IEntityTypeConfiguration<InventoryBalance>
{
    public void Configure(EntityTypeBuilder<InventoryBalance> builder)
    {
        builder.HasKey(balance => new { balance.TenantId, balance.BranchId, balance.SkuId });
        builder.Property(balance => balance.AvgCost).HasPrecision(18, 4);
        // Hàng rào cuối ở database (docs/architecture/transactions-and-concurrency.md): lỗi lập trình thành lỗi rõ ràng.
        builder.ToTable(table => table.HasCheckConstraint(
            "ck_balance_non_negative", "on_hand >= 0 AND reserved >= 0 AND reserved <= on_hand"));
    }
}

// Trigger chặn UPDATE và DELETE nằm trong migration tạo bảng.
internal sealed class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
    {
        builder.HasKey(line => line.Seq);
        builder.HasIndex(line => line.Id).IsUnique();
        builder.Property(line => line.Type).HasConversion<string>();
        builder.Property(line => line.Reason).HasConversion<string>();
        builder.Property(line => line.ReferenceType).HasConversion<string>();
        builder.Property(line => line.UnitCost).HasPrecision(18, 4);

        builder.HasIndex(line => new { line.TenantId, line.CreatedAt });
        builder.HasIndex(line => new { line.TenantId, line.BranchId, line.SkuId, line.Seq });
        builder.HasIndex(line => new { line.TenantId, line.ReferenceType, line.ReferenceId });
    }
}

// Bảng và ràng buộc: docs/design/data-model/core.md mục "Chứng từ kho và bảng phụ".
internal sealed class PurchaseReceiptConfiguration : IEntityTypeConfiguration<PurchaseReceipt>
{
    public void Configure(EntityTypeBuilder<PurchaseReceipt> builder)
    {
        builder.Property(receipt => receipt.Status).HasConversion<string>();
        builder.HasOne<Supplier>().WithMany().HasForeignKey(receipt => receipt.SupplierId).OnDelete(DeleteBehavior.Restrict);
        // Dòng phiếu thuộc trọn về phiếu: sửa phiếu Draft là thay cả bộ dòng.
        builder.HasMany(receipt => receipt.Items).WithOne().HasForeignKey(item => item.ReceiptId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(receipt => new { receipt.TenantId, receipt.ReceiptNumber }).IsUnique();
    }
}

internal sealed class PurchaseReceiptItemConfiguration : IEntityTypeConfiguration<PurchaseReceiptItem>
{
    public void Configure(EntityTypeBuilder<PurchaseReceiptItem> builder)
    {
        // Dòng phiếu được thêm qua phiếu chứ không qua DbContext.Add; khóa do Domain đặt sẵn (docs/conventions/backend.md).
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.UnitCost).HasPrecision(18, 4);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_purchase_receipt_items_quantity_positive", "quantity > 0");
            table.HasCheckConstraint("ck_purchase_receipt_items_unit_cost_non_negative", "unit_cost >= 0");
        });
    }
}
