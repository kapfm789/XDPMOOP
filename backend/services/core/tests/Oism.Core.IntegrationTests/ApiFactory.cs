using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Oism.Core.IntegrationTests;

// Chạy Api thật trên PostgreSQL và RabbitMQ thật. Lớp test dùng qua IClassFixture<ApiFactory>.
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public RabbitMqContainer Broker { get; } = new RabbitMqBuilder("rabbitmq:4-management-alpine").Build();

    public Task InitializeAsync() => Task.WhenAll(_postgres.StartAsync(), Broker.StartAsync());

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await Broker.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder
        .UseSetting("ConnectionStrings:Oism", _postgres.GetConnectionString())
        .UseSetting("ConnectionStrings:RabbitMq", Broker.GetConnectionString());
}
