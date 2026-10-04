using System.Net;
using System.Net.Http.Json;

namespace Oism.Identity.IntegrationTests.Tenancy;

// T14 cho người dùng: Owner của tenant A không đọc và không sửa được người dùng của tenant B.
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
            $"/users/{staffOfB.Id}", new { fullName = "Bị đổi", role = "Cashier", branchId = Guid.NewGuid(), isActive = false });

        Assert.Equal([tenantA.OwnerId], listOfA.Items.Select(user => user.Id));
        Assert.Equal(1, listOfA.Total);
        await update.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        Assert.Contains(staffOfB, (await ownerB.ListUsersAsync()).Items);
    }
}
