namespace Oism.SharedKernel;

// Entity nghiệp vụ cài interface này để DbContext base tự lọc và tự gán TenantId.
// Khai báo `public Guid TenantId { get; private set; }` để EF Core ánh xạ được.
public interface ITenantOwned
{
    Guid TenantId { get; }
}
