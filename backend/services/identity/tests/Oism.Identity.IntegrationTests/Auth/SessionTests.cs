using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Oism.Identity.Application.Auth;
using Oism.Identity.Domain;
using Oism.Identity.Infrastructure;

namespace Oism.Identity.IntegrationTests.Auth;

// UC-AUTH-02: đăng nhập, làm mới phiên, đăng xuất.
public sealed class SessionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _anonymous = factory.CreateClient();

    [Fact]
    [Trait("UseCase", "UC-AUTH-02 AC-1")]
    public async Task Login_ByEmailOrPhone_ReturnsSignedTokenWithClaimsAndRefreshToken()
    {
        var tenant = await _anonymous.RegisterTenantAsync();

        var byEmail = await _anonymous.LoginAsync(tenant.Email.ToUpperInvariant());
        var byPhone = await _anonymous.LoginAsync(tenant.Phone);

        // Kiểm chữ ký bằng khóa công khai, như gateway và các service khác sẽ làm.
        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(byEmail.AccessToken, new TokenValidationParameters
        {
            IssuerSigningKey = factory.PublicKey,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ValidateIssuer = false,
            ValidateAudience = false,
        });
        Assert.True(validation.IsValid, validation.Exception?.Message);

        var token = new JsonWebToken(byEmail.AccessToken);
        Assert.Equal(tenant.OwnerId.ToString(), token.GetClaim("sub").Value);
        Assert.Equal(tenant.TenantId.ToString(), token.GetClaim("tenant_id").Value);
        Assert.Equal("Owner", token.GetClaim("role").Value);
        Assert.False(token.TryGetClaim("branch_id", out _));
        Assert.Equal(TimeSpan.FromMinutes(60), token.ValidTo - token.IssuedAt);
        Assert.Equal(3600, byEmail.ExpiresIn);
        Assert.NotEmpty(byEmail.RefreshToken);
        Assert.Equal(new AuthUser(tenant.OwnerId, "Chủ cửa hàng", "Owner", null), byEmail.User);
        Assert.Equal(byEmail.User, byPhone.User);
        Assert.NotEqual(byEmail.RefreshToken, byPhone.RefreshToken);
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-02 AC-2")]
    public async Task Login_WrongPasswordOrUnknownAccount_Returns401WithTheSameBody()
    {
        var tenant = await _anonymous.RegisterTenantAsync();

        var wrongPassword = await _anonymous.PostAsJsonAsync("/auth/login", new { identifier = tenant.Email, password = "sai-mat-khau-1" });
        var unknownAccount = await _anonymous.PostAsJsonAsync("/auth/login", new { identifier = ApiExtensions.NewEmail(), password = ApiExtensions.Password });

        await wrongPassword.AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthenticated");
        await unknownAccount.AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthenticated");
        Assert.Equal(await wrongPassword.Content.ReadAsStringAsync(), await unknownAccount.Content.ReadAsStringAsync());
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-02 AC-3")]
    public async Task Refresh_ValidToken_ReturnsNewWorkingPair()
    {
        var tenant = await _anonymous.RegisterTenantAsync();
        var login = await _anonymous.LoginAsync(tenant.Email);

        var response = await _anonymous.RefreshAsync(login.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshed = (await response.Content.ReadFromJsonAsync<AuthResult>())!;
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        Assert.Equal(login.User, refreshed.User);
        Assert.Equal(HttpStatusCode.OK, (await factory.Authorized(refreshed.AccessToken).GetAsync("/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _anonymous.RefreshAsync(refreshed.RefreshToken)).StatusCode);
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-02 AC-3")]
    public async Task Refresh_UnknownOrExpiredToken_Returns401()
    {
        var tenant = await _anonymous.RegisterTenantAsync();
        var login = await _anonymous.LoginAsync(tenant.Email);

        var unknown = await _anonymous.RefreshAsync("khong-phai-token");
        // Đưa hạn của token về quá khứ thay vì chờ 7 ngày.
        using (var scope = factory.Services.CreateScope())
        {
            var hash = RefreshToken.Hash(login.RefreshToken);
            await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().RefreshTokens.IgnoreQueryFilters()
                .Where(token => token.TokenHash == hash)
                .ExecuteUpdateAsync(update => update.SetProperty(token => token.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        }

        var expired = await _anonymous.RefreshAsync(login.RefreshToken);

        await unknown.AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthenticated");
        await expired.AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthenticated");
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-02 AC-4")]
    public async Task Refresh_ReplacedTokenUsedAgain_Returns401AndRevokesEveryTokenOfTheUser()
    {
        var tenant = await _anonymous.RegisterTenantAsync();
        var firstSession = await _anonymous.LoginAsync(tenant.Email);
        var otherSession = await _anonymous.LoginAsync(tenant.Email);
        var rotated = (await (await _anonymous.RefreshAsync(firstSession.RefreshToken)).Content.ReadFromJsonAsync<AuthResult>())!;

        var reused = await _anonymous.RefreshAsync(firstSession.RefreshToken);

        await reused.AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthenticated");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymous.RefreshAsync(rotated.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymous.RefreshAsync(otherSession.RefreshToken)).StatusCode);
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-02 AC-5")]
    public async Task Logout_RevokesTheRefreshTokenAndCanBeRepeated()
    {
        var tenant = await _anonymous.RegisterTenantAsync();
        var login = await _anonymous.LoginAsync(tenant.Email);
        var otherSession = await _anonymous.LoginAsync(tenant.Email);
        var client = factory.Authorized(login.AccessToken);

        var logout = await client.PostAsJsonAsync("/auth/logout", new { refreshToken = login.RefreshToken });
        var again = await client.PostAsJsonAsync("/auth/logout", new { refreshToken = login.RefreshToken });

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymous.RefreshAsync(login.RefreshToken)).StatusCode);
        // Token đã đăng xuất chưa từng bị xoay vòng, nên dùng lại nó không kéo theo phiên khác.
        Assert.Equal(HttpStatusCode.OK, (await _anonymous.RefreshAsync(otherSession.RefreshToken)).StatusCode);
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-02 AC-5")]
    public async Task Logout_WithoutAccessTokenOrWithSomeoneElsesRefreshToken_LeavesThatTokenUsable()
    {
        var victim = await _anonymous.RegisterTenantAsync();
        var victimLogin = await _anonymous.LoginAsync(victim.Email);
        var attacker = await factory.LoginAsOwnerAsync(await _anonymous.RegisterTenantAsync());

        var anonymous = await _anonymous.PostAsJsonAsync("/auth/logout", new { refreshToken = victimLogin.RefreshToken });
        var foreign = await attacker.PostAsJsonAsync("/auth/logout", new { refreshToken = victimLogin.RefreshToken });

        await anonymous.AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthenticated");
        Assert.Equal(HttpStatusCode.NoContent, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _anonymous.RefreshAsync(victimLogin.RefreshToken)).StatusCode);
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-02 AC-6")]
    public async Task LoginOrRefresh_DeactivatedUser_Returns401()
    {
        var owner = await factory.LoginAsOwnerAsync(await _anonymous.RegisterTenantAsync());
        var email = ApiExtensions.NewEmail();
        var staff = await owner.CreateUserAsync("Staff", email);
        var staffLogin = await _anonymous.LoginAsync(email);

        var deactivate = await owner.PutAsJsonAsync(
            $"/users/{staff.Id}", new { fullName = staff.FullName, role = "Staff", isActive = false });

        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        var login = await _anonymous.PostAsJsonAsync("/auth/login", new { identifier = email, password = ApiExtensions.Password });
        await login.AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthenticated");
        await (await _anonymous.RefreshAsync(staffLogin.RefreshToken)).AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthenticated");
    }
}
