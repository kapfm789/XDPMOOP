using System.Net;
using System.Net.Http.Json;

namespace Oism.Identity.IntegrationTests.Tenancy;

// T14 cho identity: Owner của tenant A không đọc, không sửa và không gắn quan hệ được với dữ liệu của tenant B.
public sealed class TenantIsolationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _anonymous = factory.CreateClient();

    [Fact]
    [Trait("Scenario", "T14")]
    [Trait("UseCase", "UC-AUTH-01 AC-3")]
    public async Task Users_OfAnotherTenant_AreNeitherListedNorUpdatable()
    {
        var tenantA = await _anonymous.RegisterTenantAsync("Tenant A");
        var tenantB = await _anonymous.RegisterTenantAsync("Tenant B");
        var ownerA = await factory.LoginAsOwnerAsync(tenantA);
        var ownerB = await factory.LoginAsOwnerAsync(tenantB);
        var staffOfB = await ownerB.CreateUserAsync("Staff", ApiExtensions.NewEmail());

        var listOfA = await ownerA.ListUsersAsync();
        var update = await ownerA.PutAsJsonAsync(
            $"/users/{staffOfB.Id}", new { fullName = "Bị đổi", role = "Staff", isActive = false });

        Assert.Equal([tenantA.OwnerId], listOfA.Items.Select(user => user.Id));
        Assert.Equal(1, listOfA.Total);
        await update.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        Assert.Contains(staffOfB, (await ownerB.ListUsersAsync()).Items);
    }

    [Fact]
    [Trait("Scenario", "T14")]
    [Trait("UseCase", "UC-AUTH-01 AC-3")]
    public async Task Branches_OfAnotherTenant_AreNeitherListedNorUpdatableNorAttachable()
    {
        var tenantA = await _anonymous.RegisterTenantAsync("Tenant A");
        var tenantB = await _anonymous.RegisterTenantAsync("Tenant B");
        var ownerA = await factory.LoginAsOwnerAsync(tenantA);
        var ownerB = await factory.LoginAsOwnerAsync(tenantB);
        var branchOfB = await ownerB.CreateBranchAsync("CH-B");
        var staffOfA = await ownerA.CreateUserAsync("Staff", ApiExtensions.NewEmail());

        var update = await ownerA.PutAsJsonAsync($"/branches/{branchOfB.Id}", new { name = "Bị đổi", type = "Warehouse" });
        var toggle = await ownerA.PatchAsJsonAsync($"/branches/{branchOfB.Id}/active", new { isActive = false });
        // Gắn người dùng của A vào chi nhánh của B: không quan hệ chéo nào được tạo.
        var createCashier = await ownerA.PostAsJsonAsync("/users", new
        {
            fullName = "Thu ngân", email = ApiExtensions.NewEmail(), password = ApiExtensions.Password, role = "Cashier", branchId = branchOfB.Id,
        });
        var moveStaff = await ownerA.PutAsJsonAsync(
            $"/users/{staffOfA.Id}", new { fullName = staffOfA.FullName, role = "Cashier", branchId = branchOfB.Id, isActive = true });

        await update.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await toggle.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await createCashier.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await moveStaff.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        Assert.Empty(await ownerA.ListBranchesAsync());
        Assert.Equal(2, (await ownerA.ListUsersAsync()).Total);
        Assert.Contains(staffOfA, (await ownerA.ListUsersAsync()).Items);
        Assert.Equal([branchOfB], await ownerB.ListBranchesAsync());
        // Dữ liệu của B không đổi nên B không phát thêm thông điệp nào ngoài lần tạo; A không phát gì.
        Assert.Equal(1, Assert.Single(await factory.BranchEventsAsync(tenantB.TenantId)).Version);
        Assert.Empty(await factory.BranchEventsAsync(tenantA.TenantId));
    }
}
