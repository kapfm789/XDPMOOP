using System.Security.Cryptography;
using System.Text;
using Oism.SharedKernel;

namespace Oism.Identity.Domain;

public sealed class RefreshToken : ITenantOwned
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private RefreshToken()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? ReplacedById { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    // Đã bị xoay vòng sang token khác. Token như vậy mà còn được gửi lên là dấu hiệu nó bị lộ.
    public bool WasReplaced => ReplacedById is not null;

    // `token` là giá trị trao cho người dùng; database chỉ giữ giá trị băm.
    public static RefreshToken Issue(Guid userId, DateTimeOffset now, out string token)
    {
        token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Hash(token),
            ExpiresAt = now + Lifetime,
            CreatedAt = now,
        };
    }

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public bool IsUsable(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    public void ReplaceWith(RefreshToken next, DateTimeOffset now)
    {
        RevokedAt = now;
        ReplacedById = next.Id;
    }
}
