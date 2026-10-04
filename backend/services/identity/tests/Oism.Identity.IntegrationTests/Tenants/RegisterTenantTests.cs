using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Oism.Identity.Application.Auth.GetMe;
using Oism.Identity.Infrastructure;

namespace Oism.Identity.IntegrationTests.Tenants;

// UC-AUTH-01: đăng ký tenant.
public sealed class RegisterTenantTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _anonymous = factory.CreateClient();

    [Fact]
    [Trait("UseCase", "UC-AUTH-01 AC-1")]
    public async Task RegisterTenant_ValidInput_CreatesTenantWithItsOwner()
    {
        var tenant = await _anonymous.RegisterTenantAsync("Cửa hàng Hoa Mai");

        var login = await _anonymous.LoginAsync(tenant.Email);
        var me = await factory.Authorized(login.AccessToken).GetFromJsonAsync<MeDto>("/me");

        Assert.NotEqual(Guid.Empty, tenant.TenantId);
        Assert.Equal(tenant.OwnerId, login.User.Id);
        Assert.Equal("Owner", login.User.Role);
        Assert.Equal(new MeDto(tenant.OwnerId, "Chủ cửa hàng", "Owner", null, "Cửa hàng Hoa Mai"), me);
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-01 AC-2")]
    public async Task RegisterTenant_EmailOrPhoneAlreadyUsed_Returns409AndCreatesNothing()
    {
        var existing = await _anonymous.RegisterTenantAsync();
        var (tenantsBefore, usersBefore) = await CountAsync();
        var (newEmail, newPhone) = (ApiExtensions.NewEmail(), ApiExtensions.NewPhone());

        // Email so khớp không phân biệt hoa thường.
        var sameEmail = await RegisterAsync(email: existing.Email.ToUpperInvariant(), phone: newPhone);
        var samePhone = await RegisterAsync(email: newEmail, phone: existing.Phone);

        await sameEmail.AssertProblemAsync(HttpStatusCode.Conflict, "duplicate");
        await samePhone.AssertProblemAsync(HttpStatusCode.Conflict, "duplicate");
        Assert.Equal((tenantsBefore, usersBefore), await CountAsync());
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-01 AC-4")]
    public async Task RegisterTenant_Password_IsStoredAsBcryptHashAndNeverReturned()
    {
        var email = ApiExtensions.NewEmail();

        var register = await RegisterAsync(email, phone: null);
        var login = await _anonymous.PostAsJsonAsync("/auth/login", new { identifier = email, password = ApiExtensions.Password });
        var owner = factory.Authorized((await login.Content.ReadFromJsonAsync<Application.Auth.AuthResult>())!.AccessToken);
        var bodies = new[]
        {
            await register.Content.ReadAsStringAsync(),
            await login.Content.ReadAsStringAsync(),
            await owner.GetStringAsync("/me"),
            await owner.GetStringAsync("/users"),
        };

        using var scope = factory.Services.CreateScope();
        var hash = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.IgnoreQueryFilters()
            .Where(user => user.Email == email).Select(user => user.PasswordHash).SingleAsync();
        Assert.StartsWith("$2", hash);
        Assert.DoesNotContain(ApiExtensions.Password, hash);
        Assert.All(bodies, body =>
        {
            Assert.DoesNotContain(hash, body);
            Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-01 AC-1")]
    public async Task RegisterTenant_PhoneOnly_OwnerLogsInByPhone()
    {
        var phone = ApiExtensions.NewPhone();

        var register = await RegisterAsync(email: " ", phone);

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        Assert.Equal("Owner", (await _anonymous.LoginAsync(phone)).User.Role);
    }

    [Theory]
    [InlineData(null, null, "mat-khau-thu-123", "email")]
    [InlineData("khong-phai-email", null, "mat-khau-thu-123", "email")]
    [InlineData(null, "12ab", "mat-khau-thu-123", "phone")]
    [InlineData("a@test.local", null, "ngan", "password")]
    [Trait("UseCase", "UC-AUTH-01 AC-1")]
    public async Task RegisterTenant_InvalidInput_Returns400NamingTheField(string? email, string? phone, string password, string field)
    {
        var response = await _anonymous.PostAsJsonAsync(
            "/tenants", new { tenantName = "Cửa hàng", ownerName = "Chủ", email, phone, password });

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), problem.ToString());
    }

    private Task<HttpResponseMessage> RegisterAsync(string? email, string? phone) => _anonymous.PostAsJsonAsync(
        "/tenants", new { tenantName = "Cửa hàng khác", ownerName = "Chủ khác", email, phone, password = ApiExtensions.Password });

    private async Task<(int Tenants, int Users)> CountAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        return (await db.Tenants.CountAsync(), await db.Users.IgnoreQueryFilters().CountAsync());
    }
}
