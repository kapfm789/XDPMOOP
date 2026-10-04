using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Oism.BuildingBlocks.Tenancy;
using Oism.Identity.Application;
using Oism.Identity.Domain;

namespace Oism.Identity.Infrastructure.Security;

internal sealed class JwtAccessTokenIssuer(IConfiguration configuration) : IAccessTokenIssuer
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(60);

    private readonly Lazy<SigningCredentials> _credentials = new(() => LoadPrivateKey(configuration["Jwt:PrivateKey"]));

    public AccessToken Issue(User user, DateTimeOffset now)
    {
        var claims = new Dictionary<string, object>
        {
            ["sub"] = user.Id.ToString(),
            [TenantMiddleware.TenantIdClaim] = user.TenantId.ToString(),
            ["role"] = user.Role.ToString(),
        };
        if (user.BranchId is { } branchId)
            claims["branch_id"] = branchId.ToString();

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Claims = claims,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = (now + Lifetime).UtcDateTime,
            SigningCredentials = _credentials.Value,
        });
        return new AccessToken(token, (int)Lifetime.TotalSeconds);
    }

    // Khóa bí mật RSA dạng PKCS#8, base64 trên một dòng (phần thân của file PEM "PRIVATE KEY").
    // Chỉ identity giữ khóa này; gateway và service khác chỉ nhận khóa công khai qua Jwt:PublicKey.
    private static SigningCredentials LoadPrivateKey(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
            throw new InvalidOperationException("Jwt:PrivateKey is not configured; identity cannot sign access tokens.");

        var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(base64), out _);
        return new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
    }
}
