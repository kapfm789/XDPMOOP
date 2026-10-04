using Oism.Identity.Domain;

namespace Oism.Identity.Application.Auth;

public interface IRefreshTokenRepository
{
    // Làm mới phiên khi access token đã hết hạn nên chưa biết tenant: tìm theo giá trị băm trên mọi tenant,
    // khóa dòng, rồi đặt tenant context theo bản ghi tìm được.
    Task<RefreshToken?> FindForUpdateAsync(string tokenHash, CancellationToken ct);

    // Trong tenant của người đang đăng nhập.
    Task<RefreshToken?> GetAsync(string tokenHash, CancellationToken ct);

    Task RevokeAllAsync(Guid userId, DateTimeOffset now, CancellationToken ct);

    void Add(RefreshToken token);
}
