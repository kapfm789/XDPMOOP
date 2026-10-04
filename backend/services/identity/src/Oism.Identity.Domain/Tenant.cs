namespace Oism.Identity.Domain;

public enum TenantStatus
{
    Active,
    Suspended,
}

// Bảng duy nhất không có tenant_id: Id của nó chính là TenantId.
public sealed class Tenant(string name, DateTimeOffset createdAt)
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string Name { get; private set; } = name;

    public TenantStatus Status { get; private set; } = TenantStatus.Active;

    public DateTimeOffset CreatedAt { get; private set; } = createdAt;
}
