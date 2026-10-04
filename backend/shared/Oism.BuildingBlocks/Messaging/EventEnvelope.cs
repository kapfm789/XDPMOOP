using System.Text.Json;

namespace Oism.BuildingBlocks.Messaging;

// Phong bì chung của mọi thông điệp: docs/architecture/messaging.md mục "Phong bì chung".
public sealed record EventEnvelope(Guid EventId, string Type, Guid TenantId, DateTimeOffset OccurredAt, JsonElement Payload)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
