using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oism.Core.Domain.Orders;

namespace Oism.Core.Infrastructure.Configurations;

// Bảng và ràng buộc: docs/design/data-model/core.md mục "orders" và "order_items".
internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.Property(order => order.Channel).HasConversion<string>();
        builder.Property(order => order.Status).HasConversion<string>();
        builder.Property(order => order.CancelReason).HasConversion<string>();
        builder.Property(order => order.TotalAmount).HasPrecision(18, 4);
        builder.HasMany(order => order.Items).WithOne().HasForeignKey(item => item.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(order => order.Payment).WithOne().HasForeignKey<Payment>(payment => payment.OrderId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(order => new { order.TenantId, order.OrderNumber }).IsUnique();
        // Chặn cùng một đơn của sàn tới hai lần, và bấm thanh toán hai lần ở POS.
        builder.HasIndex(order => new { order.TenantId, order.Channel, order.ExternalOrderId })
            .IsUnique().HasFilter("external_order_id IS NOT NULL");
        builder.HasIndex(order => new { order.TenantId, order.IdempotencyKey })
            .IsUnique().HasFilter("idempotency_key IS NOT NULL");
        builder.HasIndex(order => new { order.TenantId, order.CreatedAt });
        // Cho job hết hạn giữ hàng.
        builder.HasIndex(order => new { order.TenantId, order.Status, order.ReservedUntil });
    }
}

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        // Dòng đơn được thêm qua đơn chứ không qua DbContext.Add; khóa do Domain đặt sẵn (docs/conventions/backend.md).
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.UnitPrice).HasPrecision(18, 4);
        builder.Property(item => item.Discount).HasPrecision(18, 4);
        builder.Property(item => item.CostPrice).HasPrecision(18, 4);

        builder.HasIndex(item => new { item.TenantId, item.OrderId });
        builder.HasIndex(item => new { item.TenantId, item.SkuId });
    }
}

// Bảng và chỉ mục: docs/design/data-model/core.md mục "reservations".
internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.Property(reservation => reservation.Status).HasConversion<string>();
        builder.HasOne<Order>().WithMany().HasForeignKey(reservation => reservation.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrderItem>().WithMany().HasForeignKey(reservation => reservation.OrderItemId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(reservation => new { reservation.TenantId, reservation.OrderId });
        // Cho job hết hạn quét mọi tenant.
        builder.HasIndex(reservation => new { reservation.Status, reservation.ExpiresAt });
    }
}

// Bảng và ràng buộc: docs/design/data-model/core.md mục "Chứng từ kho và bảng phụ".
internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        // Thanh toán được thêm qua đơn chứ không qua DbContext.Add; khóa do Domain đặt sẵn (docs/conventions/backend.md).
        builder.Property(payment => payment.Id).ValueGeneratedNever();
        builder.Property(payment => payment.Method).HasConversion<string>();
        builder.Property(payment => payment.Amount).HasPrecision(18, 4);

        builder.HasIndex(payment => new { payment.TenantId, payment.OrderId }).IsUnique();
    }
}
