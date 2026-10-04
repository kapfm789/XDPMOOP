using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Oism.Core.IntegrationTests;

// Chạy Api thật trên PostgreSQL và RabbitMQ thật. Lớp test dùng qua IClassFixture<ApiFactory>.
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly RSA _rsa = RSA.Create(2048);

    public RabbitMqContainer Broker { get; } = new RabbitMqBuilder("rabbitmq:4-management-alpine").Build();

    public Task InitializeAsync() => Task.WhenAll(_postgres.StartAsync(), Broker.StartAsync());

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await Broker.DisposeAsync();
        await _postgres.DisposeAsync();
        _rsa.Dispose();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder
        .UseSetting("ConnectionStrings:Oism", _postgres.GetConnectionString())
        .UseSetting("ConnectionStrings:RabbitMq", Broker.GetConnectionString())
        .UseSetting("Jwt:PublicKey", Convert.ToBase64String(_rsa.ExportSubjectPublicKeyInfo()));

    // Client mang JWT như identity cấp: claim sub, tenant_id, role (docs/architecture/security.md).
    public HttpClient CreateClient(string role, Guid tenantId)
    {
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object>
            {
                ["sub"] = Guid.NewGuid().ToString(),
                ["tenant_id"] = tenantId.ToString(),
                ["role"] = role,
            },
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(_rsa), SecurityAlgorithms.RsaSha256),
        });

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
