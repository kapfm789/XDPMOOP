using Oism.Identity.Domain;

namespace Oism.Identity.Application.Users;

public interface IUserRepository
{
    // Đăng nhập chưa có token nên chưa biết tenant: tìm theo email hoặc số điện thoại trên mọi tenant,
    // rồi đặt tenant context theo người dùng tìm được.
    Task<User?> FindByIdentifierAsync(string identifier, CancellationToken ct);

    Task<User?> GetAsync(Guid id, CancellationToken ct);

    // Người dùng của tenant, mới nhất trước.
    Task<PagedResult<UserDto>> ListAsync(int page, int pageSize, CancellationToken ct);

    void Add(User user);
}
