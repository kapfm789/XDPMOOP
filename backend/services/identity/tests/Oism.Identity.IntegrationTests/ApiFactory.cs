using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;

namespace Oism.Identity.IntegrationTests;

// Chạy Api thật trên PostgreSQL thật. Lớp test dùng qua IClassFixture<ApiFactory>.
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly RSA _rsa = RSA.Create(2048);

    // Khóa công khai mà gateway và các service khác dùng để kiểm token do identity ký.
    public RsaSecurityKey PublicKey => new(_rsa.ExportParameters(includePrivateParameters: false));

    public Task InitializeAsync() => _postgres.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        _rsa.Dispose();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder
        .UseSetting("ConnectionStrings:Oism", _postgres.GetConnectionString())
        .UseSetting("Jwt:PrivateKey", Convert.ToBase64String(_rsa.ExportPkcs8PrivateKey()))
        .UseSetting("Jwt:PublicKey", Convert.ToBase64String(_rsa.ExportSubjectPublicKeyInfo()));
}
