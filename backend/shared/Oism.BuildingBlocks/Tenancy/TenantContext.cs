namespace Oism.BuildingBlocks.Tenancy;

public interface ITenantContext
{
    Guid? TenantId { get; }

    void Set(Guid tenantId);
}

// Scoped: một instance cho mỗi request hoặc mỗi event đang xử lý.
public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }

    public void Set(Guid tenantId) => TenantId = tenantId;
}
