using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Oism.BuildingBlocks.Auth;

public static class Roles
{
    public const string Owner = "Owner";
    public const string Staff = "Staff";
    public const string Cashier = "Cashier";
}

// Bảng quyền gốc: docs/architecture/security.md.
public static class Policies
{
    public const string Owner = nameof(Owner);
    public const string OwnerOrStaff = nameof(OwnerOrStaff);
    public const string OwnerOrCashier = nameof(OwnerOrCashier);
    public const string AnyRole = nameof(AnyRole);
}

public static class OismAuth
{
    public static IServiceCollection AddOismAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                // Thiếu khóa thì không token nào hợp lệ: mọi endpoint có policy trả 401.
                IssuerSigningKey = LoadPublicKey(configuration["Jwt:PublicKey"]),
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ValidateIssuer = false,
                ValidateAudience = false,
                NameClaimType = "sub",
                RoleClaimType = "role",
            };
        });

        services.AddAuthorizationBuilder()
            // Endpoint không khai báo policy thì bị từ chối.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAssertion(_ => false).Build())
            .AddPolicy(Policies.Owner, policy => policy.RequireRole(Roles.Owner))
            .AddPolicy(Policies.OwnerOrStaff, policy => policy.RequireRole(Roles.Owner, Roles.Staff))
            .AddPolicy(Policies.OwnerOrCashier, policy => policy.RequireRole(Roles.Owner, Roles.Cashier))
            .AddPolicy(Policies.AnyRole, policy => policy.RequireRole(Roles.Owner, Roles.Staff, Roles.Cashier));

        return services;
    }

    // Khóa công khai RSA dạng SubjectPublicKeyInfo, base64 trên một dòng (phần thân của file PEM).
    private static RsaSecurityKey? LoadPublicKey(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
            return null;

        var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(base64), out _);
        return new RsaSecurityKey(rsa);
    }
}
