using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Oism.Gateway.IntegrationTests;

// W5-16: gateway giới hạn tần suất gọi POST /api/identity/auth/login theo IP client (docs/architecture/security.md).
// Phía sau gateway là một identity giả đếm số request nó nhận được.
[Trait("UseCase", "UC-AUTH-02")]
public sealed class LoginRateLimitTests : IAsyncLifetime
{
    private const int PermitLimit = 3;

    private WebApplication _identity = null!;
    private WebApplicationFactory<Program> _gateway = null!;
    private HttpClient _client = null!;
    private int _loginCalls;
    private int _refreshCalls;

    public async Task InitializeAsync()
    {
        var identity = WebApplication.CreateBuilder();
        identity.WebHost.UseUrls("http://127.0.0.1:0");
        _identity = identity.Build();
        _identity.MapPost("/auth/login", () => { Interlocked.Increment(ref _loginCalls); return Results.Ok(); });
        _identity.MapPost("/auth/refresh", () => { Interlocked.Increment(ref _refreshCalls); return Results.Ok(); });
        await _identity.StartAsync();
        var identityUrl = _identity.Urls.Single();

        _gateway = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReverseProxy:Clusters:identity:Destinations:default:Address"] = identityUrl,
                ["RateLimit:Login:PermitLimit"] = PermitLimit.ToString(),
                ["RateLimit:Login:WindowSeconds"] = "60",
            }));
        });
        _client = _gateway.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _gateway.DisposeAsync();
        await _identity.DisposeAsync();
    }

    [Fact]
    public async Task Login_UpToTheLimit_IsForwardedToIdentity()
    {
        for (var i = 0; i < PermitLimit; i++)
        {
            var response = await Login();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        Assert.Equal(PermitLimit, _loginCalls);
    }

    [Fact]
    public async Task Login_OverTheLimitInOneWindow_Returns429WithRetryAfterAndIsNotForwarded()
    {
        for (var i = 0; i < PermitLimit; i++)
            await Login();

        var rejected = await Login();

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.RetryAfter?.Delta > TimeSpan.Zero, "Thiếu header Retry-After");
        using var body = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.Equal("too_many_requests", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(429, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(PermitLimit, _loginCalls);
    }

    [Fact]
    public async Task Refresh_ManyTimes_IsNotLimited()
    {
        for (var i = 0; i < PermitLimit * 3; i++)
        {
            var response = await _client.PostAsJsonAsync("/api/identity/auth/refresh", new { refreshToken = "x" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        Assert.Equal(PermitLimit * 3, _refreshCalls);
    }

    [Fact]
    public async Task Login_AfterOtherRoutesUsedTheirShare_StillHasItsOwnLimit()
    {
        for (var i = 0; i < PermitLimit * 2; i++)
            await _client.PostAsJsonAsync("/api/identity/auth/refresh", new { refreshToken = "x" });

        var response = await Login();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private Task<HttpResponseMessage> Login() =>
        _client.PostAsJsonAsync("/api/identity/auth/login", new { identifier = "owner@moc.test", password = "sai-mat-khau" });
}
