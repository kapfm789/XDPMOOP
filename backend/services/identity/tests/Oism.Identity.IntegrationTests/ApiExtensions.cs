using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Messaging;
using Oism.BuildingBlocks.Tenancy;
using Oism.Contracts;
using Oism.Identity.Application;
using Oism.Identity.Application.Auth;
using Oism.Identity.Application.Branches;
using Oism.Identity.Application.Tenants.RegisterTenant;
using Oism.Identity.Application.Users;
using Oism.Identity.Infrastructure;

namespace Oism.Identity.IntegrationTests;

// Một tenant vừa đăng ký, kèm thông tin đăng nhập của Owner.
internal sealed record TestTenant(Guid TenantId, Guid OwnerId, string Email, string Phone);

internal static class ApiExtensions
{
    // Giá trị thử, không phải mật khẩu thật.
    public const string Password = "mat-khau-thu-123";

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@test.local";

    public static string NewPhone() => $"09{Random.Shared.NextInt64(10_000_000, 99_999_999)}";

    public static async Task<TestTenant> RegisterTenantAsync(this HttpClient client, string tenantName = "Cửa hàng thử")
    {
        var (email, phone) = (NewEmail(), NewPhone());
        var response = await client.PostAsJsonAsync(
            "/tenants", new { tenantName, ownerName = "Chủ cửa hàng", email, phone, password = Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = (await response.Content.ReadFromJsonAsync<RegisterTenantResult>())!;
        return new TestTenant(result.TenantId, result.UserId, email, phone);
    }

    public static async Task<AuthResult> LoginAsync(this HttpClient client, string identifier, string password = Password)
    {
        var response = await client.PostAsJsonAsync("/auth/login", new { identifier, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResult>())!;
    }

    public static Task<HttpResponseMessage> RefreshAsync(this HttpClient client, string refreshToken) =>
        client.PostAsJsonAsync("/auth/refresh", new { refreshToken });

    // Client mang access token do chính identity cấp.
    public static HttpClient Authorized(this ApiFactory factory, string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public static async Task<HttpClient> LoginAsOwnerAsync(this ApiFactory factory, TestTenant tenant) =>
        factory.Authorized((await factory.CreateClient().LoginAsync(tenant.Email)).AccessToken);

    public static async Task<UserDto> CreateUserAsync(this HttpClient owner, string role, string identifier, Guid? branchId = null)
    {
        var isEmail = identifier.Contains('@');
        var response = await owner.PostAsJsonAsync("/users", new
        {
            fullName = $"{role} thử",
            email = isEmail ? identifier : null,
            phone = isEmail ? null : identifier,
            password = Password,
            role,
            branchId,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<UserDto>())!;
    }

    public static async Task<PagedResult<UserDto>> ListUsersAsync(this HttpClient owner, string query = "") =>
        (await owner.GetFromJsonAsync<PagedResult<UserDto>>($"/users{query}"))!;

    public static async Task<BranchDto> CreateBranchAsync(
        this HttpClient owner, string code, string type = "Store", string name = "Chi nhánh thử", string? address = null)
    {
        var response = await owner.PostAsJsonAsync("/branches", new { code, name, type, address });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<BranchDto>())!;
    }

    public static async Task<IReadOnlyList<BranchDto>> ListBranchesAsync(this HttpClient client, string query = "") =>
        (await client.GetFromJsonAsync<IReadOnlyList<BranchDto>>($"/branches{query}"))!;

    // Các BranchUpserted tenant đã ghi vào outbox, theo thứ tự version.
    public static async Task<IReadOnlyList<BranchUpserted>> BranchEventsAsync(this ApiFactory factory, Guid tenantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
        var payloads = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Set<OutboxMessage>()
            .Where(message => message.Type == nameof(BranchUpserted))
            .Select(message => message.Payload)
            .ToListAsync();
        return payloads
            .Select(payload => JsonSerializer.Deserialize<BranchUpserted>(payload, EventEnvelope.JsonOptions)!)
            .OrderBy(message => message.BranchId).ThenBy(message => message.Version)
            .ToList();
    }

    // Kiểm mã HTTP và trường `code` của ProblemDetails (docs/design/api/README.md).
    public static async Task<JsonElement> AssertProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, problem.GetProperty("code").GetString());
        return problem;
    }
}
