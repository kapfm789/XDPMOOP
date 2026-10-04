using System.Net;
using System.Net.Http.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Oism.Identity.Application.Users;

namespace Oism.Identity.IntegrationTests.Users;

// UC-AUTH-03: quản lý người dùng và vai trò.
public sealed class UserTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _anonymous = factory.CreateClient();

    [Fact]
    [Trait("UseCase", "UC-AUTH-03 AC-1")]
    public async Task CreateUser_StaffAndCashier_BelongToTheOwnersTenant()
    {
        var tenant = await _anonymous.RegisterTenantAsync();
        var owner = await factory.LoginAsOwnerAsync(tenant);
        var (staffEmail, cashierPhone, branchId) = (ApiExtensions.NewEmail(), ApiExtensions.NewPhone(), Guid.NewGuid());

        var staff = await owner.CreateUserAsync("Staff", staffEmail);
        var cashier = await owner.CreateUserAsync("Cashier", cashierPhone, branchId);

        Assert.Equal(("Staff", staffEmail, true), (staff.Role, staff.Email, staff.IsActive));
        Assert.Equal(("Cashier", cashierPhone, branchId), (cashier.Role, cashier.Phone, cashier.BranchId));
        var users = await owner.ListUsersAsync();
        Assert.Equal(3, users.Total);
        Assert.Equal(["Cashier", "Owner", "Staff"], users.Items.Select(user => user.Role).Order());

        // Cashier đăng nhập vào đúng tenant của Owner và token mang chi nhánh của mình.
        var cashierLogin = await _anonymous.LoginAsync(cashierPhone);
        var token = new JsonWebToken(cashierLogin.AccessToken);
        Assert.Equal(tenant.TenantId.ToString(), token.GetClaim("tenant_id").Value);
        Assert.Equal("Cashier", token.GetClaim("role").Value);
        Assert.Equal(branchId.ToString(), token.GetClaim("branch_id").Value);
        Assert.Equal(branchId, cashierLogin.User.BranchId);
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-03 AC-1")]
    public async Task CreateUser_InvalidInputOrDuplicate_Returns400Or409()
    {
        var tenant = await _anonymous.RegisterTenantAsync();
        var owner = await factory.LoginAsOwnerAsync(tenant);

        var cashierWithoutBranch = await CreateAsync(owner, "Cashier", ApiExtensions.NewEmail());
        var secondOwner = await CreateAsync(owner, "Owner", ApiExtensions.NewEmail());
        var duplicateEmail = await CreateAsync(owner, "Staff", tenant.Email);

        var problem = await cashierWithoutBranch.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("branchId", out _), problem.ToString());
        await secondOwner.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await duplicateEmail.AssertProblemAsync(HttpStatusCode.Conflict, "duplicate");
        Assert.Equal(1, (await owner.ListUsersAsync()).Total);
    }

    // Điều kiện xong của W1-04: Cashier gọi API quản trị nhận 403.
    [Theory]
    [InlineData("Staff")]
    [InlineData("Cashier")]
    [Trait("UseCase", "UC-AUTH-03 AC-2")]
    [Trait("UseCase", "UC-AUTH-03 AC-3")]
    public async Task Users_CalledByStaffOrCashier_Returns403(string role)
    {
        var owner = await factory.LoginAsOwnerAsync(await _anonymous.RegisterTenantAsync());
        var email = ApiExtensions.NewEmail();
        var user = await owner.CreateUserAsync(role, email, branchId: Guid.NewGuid());
        var client = factory.Authorized((await _anonymous.LoginAsync(email)).AccessToken);

        var list = await client.GetAsync("/users");
        var create = await CreateAsync(client, "Staff", ApiExtensions.NewEmail());
        var update = await client.PutAsJsonAsync($"/users/{user.Id}", new { fullName = "Tự nâng quyền", role = "Staff", isActive = true });

        await list.AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        await create.AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        await update.AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/me")).StatusCode);
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-03 AC-1")]
    public async Task UpdateUser_ChangesNameRoleBranchAndActiveFlag()
    {
        var owner = await factory.LoginAsOwnerAsync(await _anonymous.RegisterTenantAsync());
        var staff = await owner.CreateUserAsync("Staff", ApiExtensions.NewEmail());
        var branchId = Guid.NewGuid();

        var response = await owner.PutAsJsonAsync(
            $"/users/{staff.Id}", new { fullName = " Thu ngân mới ", role = "Cashier", branchId, isActive = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<UserDto>())!;
        Assert.Equal(staff with { FullName = "Thu ngân mới", Role = "Cashier", BranchId = branchId, IsActive = false }, updated);
        Assert.Contains(updated, (await owner.ListUsersAsync()).Items);
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-03 AC-1")]
    public async Task UpdateUser_UnknownIdOrOwnerAccount_Returns404Or400()
    {
        var tenant = await _anonymous.RegisterTenantAsync();
        var owner = await factory.LoginAsOwnerAsync(tenant);
        var body = new { fullName = "Tên mới", role = "Staff", isActive = true };

        var unknown = await owner.PutAsJsonAsync($"/users/{Guid.NewGuid()}", body);
        var ownerAccount = await owner.PutAsJsonAsync($"/users/{tenant.OwnerId}", body);

        await unknown.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await ownerAccount.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal("Owner", Assert.Single((await owner.ListUsersAsync()).Items).Role);
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-03 AC-1")]
    public async Task ListUsers_Paged_ReturnsRequestedPageAndTotal()
    {
        var owner = await factory.LoginAsOwnerAsync(await _anonymous.RegisterTenantAsync());
        await owner.CreateUserAsync("Staff", ApiExtensions.NewEmail());
        await owner.CreateUserAsync("Staff", ApiExtensions.NewEmail());

        var first = await owner.ListUsersAsync("?page=1&pageSize=2");
        var second = await owner.ListUsersAsync("?page=2&pageSize=2");
        var invalid = await owner.GetAsync("/users?page=0&pageSize=500");

        Assert.Equal((2, 1, 2, 3), (first.Items.Count, first.Page, first.PageSize, first.Total));
        Assert.Equal((1, 2, 3), (second.Items.Count, second.Page, second.Total));
        Assert.Empty(first.Items.Select(user => user.Id).Intersect(second.Items.Select(user => user.Id)));
        await invalid.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-02 AC-1")]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await _anonymous.GetAsync("/me");

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthenticated");
    }

    private static Task<HttpResponseMessage> CreateAsync(HttpClient client, string role, string email) =>
        client.PostAsJsonAsync("/users", new { fullName = "Người dùng", email, password = ApiExtensions.Password, role });
}
