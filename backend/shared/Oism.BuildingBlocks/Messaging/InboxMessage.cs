using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Oism.BuildingBlocks.Messaging;

// DbContext của service có nhận event cài interface này để có bảng inbox_messages.
public interface IHasInbox;

// Một thông điệp đã xử lý. Bảng hạ tầng, không có tenant_id.
public sealed class InboxMessage(Guid eventId, string type, DateTimeOffset processedAt)
{
    public Guid EventId { get; private set; } = eventId;

    public string Type { get; private set; } = type;

    public DateTimeOffset ProcessedAt { get; private set; } = processedAt;
}

internal sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");
        builder.HasKey(message => message.EventId);
        builder.Property(message => message.EventId).ValueGeneratedNever();
    }
}
