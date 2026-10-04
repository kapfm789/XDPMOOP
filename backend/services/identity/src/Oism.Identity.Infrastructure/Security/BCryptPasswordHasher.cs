using Oism.Identity.Application;

namespace Oism.Identity.Infrastructure.Security;

// BCrypt, work factor 12 (docs/architecture/security.md). Bản "enhanced" băm SHA-384 trước,
// nên mật khẩu dài hơn 72 byte không bị BCrypt cắt bớt.
internal sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password) => BCrypt.Net.BCrypt.EnhancedHashPassword(password, WorkFactor);

    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.EnhancedVerify(password, hash);
}
