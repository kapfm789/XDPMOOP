using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Oism.BuildingBlocks.Auth;
using Oism.BuildingBlocks.Tenancy;
using Oism.BuildingBlocks.Web;
using Oism.SharedKernel;

namespace Oism.BuildingBlocks.IntegrationTests.Web;

// Kiểm JWT, policy theo vai trò, tenant middleware và lỗi chuẩn trên một pipeline thật.
public sealed class PipelineTests : IAsyncLifetime
{
    private readonly RSA _rsa = RSA.Create(2048);
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration["Jwt:PublicKey"] = Convert.ToBase64String(_rsa.ExportSubjectPublicKeyInfo());
        builder.Services.AddOismWeb(builder.Configuration);

        _app = builder.Build();
        _app.UseOismWeb();
        _app.MapGet("/tenant", (ITenantContext tenant) => tenant.TenantId).RequireAuthorization(Policies.AnyRole);
        _app.MapGet("/owner", () => "ok").RequireAuthorization(Policies.Owner);
        _app.MapGet("/no-policy", () => "ok");
        _app.MapGet("/missing", string () => throw new NotFoundException("order")).AllowAnonymous();
        _app.MapGet("/crash", string () => throw new InvalidOperationException("secret detail")).AllowAnonymous();

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
        _rsa.Dispose();
    }

    [Fact]
    public async Task Request_NoToken_Returns401WithCode()
    {
        var response = await _client.GetAsync("/owner");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthenticated", await CodeAsync(response));
    }

    [Fact]
    public async Task Request_TokenSignedByAnotherKey_Returns401()
    {
        using var otherKey = RSA.Create(2048);

        var response = await GetAsync("/owner", Token(otherKey, Roles.Owner, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_RoleNotInPolicy_Returns403WithCode()
    {
        var response = await GetAsync("/owner", Token(_rsa, Roles.Staff, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("forbidden", await CodeAsync(response));
    }

    [Fact]
    public async Task Request_EndpointWithoutPolicy_IsDenied()
    {
        var response = await GetAsync("/no-policy", Token(_rsa, Roles.Owner, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Request_ValidToken_TenantContextHoldsTenantIdClaim()
    {
        var tenantId = Guid.NewGuid();

        var response = await GetAsync("/tenant", Token(_rsa, Roles.Cashier, tenantId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(tenantId, await response.Content.ReadFromJsonAsync<Guid>());
    }

    [Fact]
    public async Task Error_OismException_ReturnsProblemDetailsWithCode()
    {
        var response = await _client.GetAsync("/missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("not_found", problem.GetProperty("code").GetString());
        Assert.Equal("https://oism.local/errors/not_found", problem.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Error_UnknownException_Returns500WithoutDetail()
    {
        var response = await _client.GetAsync("/crash");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("secret detail", await response.Content.ReadAsStringAsync());
    }

    private Task<HttpResponseMessage> GetAsync(string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private static string Token(RSA key, string role, Guid tenantId) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object>
            {
                ["sub"] = Guid.NewGuid().ToString(),
                ["tenant_id"] = tenantId.ToString(),
                ["role"] = role,
            },
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(key), SecurityAlgorithms.RsaSha256),
        });

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();
}
