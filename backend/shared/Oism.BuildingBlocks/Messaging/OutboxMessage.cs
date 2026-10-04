using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oism.SharedKernel;

namespace Oism.BuildingBlocks.Messaging;

// DbContext của service có phát event cài interface này để có bảng outbox_messages.
public interface IHasOutbox;

// Một thông điệp chờ gửi. Bảng: docs/design/data-model/core.md mục "Chứng từ kho và bảng phụ".
public sealed class OutboxMessage : ITenantOwned
{
    private OutboxMessage()
    {
    }

    // Cũng là eventId của phong bì.
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Type { get; private set; } = null!;

    public string Payload { get; private set; } = null!;

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public int Attempts { get; private set; }

    // Thêm kết quả vào DbContext trong transaction của use case; TenantId do DbContext base gán khi lưu.
    public static OutboxMessage Create<TPayload>(TPayload payload, DateTimeOffset occurredAt)
        where TPayload : notnull => new()
        {
            Id = Guid.NewGuid(),
            Type = typeof(TPayload).Name,
            Payload = JsonSerializer.Serialize(payload, EventEnvelope.JsonOptions),
            OccurredAt = occurredAt.ToUniversalTime(),
        };
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.Property(message => message.Payload).HasColumnType("jsonb");
        // Tiến trình đẩy outbox chỉ quét dòng chưa gửi, theo thứ tự phát.
        builder.HasIndex(message => message.OccurredAt).HasFilter("processed_at IS NULL");
    }
}
