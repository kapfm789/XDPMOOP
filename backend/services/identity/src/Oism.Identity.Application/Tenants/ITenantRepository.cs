using Oism.Identity.Domain;

namespace Oism.Identity.Application.Tenants;

public interface ITenantRepository
{
    // Đăng ký chưa có token nên chưa có tenant context: thêm tenant rồi đặt tenant context theo nó,
    // để các bản ghi tạo trong cùng use case thuộc về tenant mới.
    void Add(Tenant tenant);

    Task<string?> GetNameAsync(Guid id, CancellationToken ct);
}
