using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oism.Channel.Domain;

namespace Oism.Channel.Infrastructure.Configurations;

// Bảng và ràng buộc: docs/design/data-model/channel.md.
internal sealed class ChannelShopConfiguration : IEntityTypeConfiguration<ChannelShop>
{
    public void Configure(EntityTypeBuilder<ChannelShop> builder)
    {
        builder.Property(shop => shop.Channel).HasConversion<string>();
        // Unique trên toàn hệ thống, không theo tenant: webhook không mang JWT nên mã shop phải chỉ tới đúng một tenant.
        builder.HasIndex(shop => new { shop.Channel, shop.ShopId }).IsUnique();
    }
}

internal sealed class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
{
    public void Configure(EntityTypeBuilder<WebhookEvent> builder)
    {
        builder.Property(webhook => webhook.Channel).HasConversion<string>();
        builder.Property(webhook => webhook.Status).HasConversion<string>();
        builder.Property(webhook => webhook.Payload).HasColumnType("jsonb");

        // Hàng rào chống trùng đầu tiên: sàn gửi lại cùng webhook (ADR-0006).
        builder.HasIndex(webhook => new { webhook.TenantId, webhook.Channel, webhook.EventId }).IsUnique();
        builder.HasIndex(webhook => new { webhook.TenantId, webhook.ReceivedAt });
        builder.HasIndex(webhook => new { webhook.TenantId, webhook.Channel, webhook.ExternalOrderId });
    }
}
