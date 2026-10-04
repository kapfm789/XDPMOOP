using System.Net;
using System.Net.Http.Json;
using Oism.Contracts;
using Oism.Identity.Application.Branches;

namespace Oism.Identity.IntegrationTests.Branches;

// UC-AUTH-04: quản lý chi nhánh.
public sealed class BranchTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _anonymous = factory.CreateClient();

    [Fact]
    [Trait("UseCase", "UC-AUTH-04 AC-1")]
    public async Task CreateBranch_StoreAndWarehouse_AreActiveAndListedByCode()
    {
        var owner = await factory.LoginAsOwnerAsync(await _anonymous.RegisterTenantAsync());

        var warehouse = await owner.CreateBranchAsync(" KHO-01 ", "Warehouse", name: " Kho tổng ", address: " 12 Lê Lợi ");
        var store = await owner.CreateBranchAsync("CH-01");

        Assert.Equal(("KHO-01", "Kho tổng", "Warehouse", "12 Lê Lợi", true),
            (warehouse.Code, warehouse.Name, warehouse.Type, warehouse.Address, warehouse.IsActive));
        Assert.Equal(("CH-01", "Store", null, true), (store.Code, store.Type, store.Address, store.IsActive));
        Assert.Equal([store, warehouse], await owner.ListBranchesAsync());
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-04 AC-2")]
    public async Task CreateBranch_CodeAlreadyInTenant_Returns409ButAnotherTenantMayUseIt()
    {
        var owner = await factory.LoginAsOwnerAsync(await _anonymous.RegisterTenantAsync());
        var otherOwner = await factory.LoginAsOwnerAsync(await _anonymous.RegisterTenantAsync());
        var first = await owner.CreateBranchAsync("CH-01");

        var duplicate = await owner.PostAsJsonAsync("/branches", new { code = "CH-01", name = "Trùng mã", type = "Store" });
        var sameCodeElsewhere = await otherOwner.CreateBranchAsync("CH-01");

        await duplicate.AssertProblemAsync(HttpStatusCode.Conflict, "duplicate");
        Assert.Equal([first], await owner.ListBranchesAsync());
        Assert.Equal([sameCodeElsewhere], await otherOwner.ListBranchesAsync());
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-04 AC-1")]
    public async Task UpdateBranch_ChangesNameTypeAndAddressButKeepsCode()
    {
        var owner = await factory.LoginAsOwnerAsync(await _anonymous.RegisterTenantAsync());
        var branch = await owner.CreateBranchAsync("CH-01", address: "Địa chỉ cũ");

        var response = await owner.PutAsJsonAsync($"/branches/{branch.Id}", new { name = " Kho quận 1 ", type = "Warehouse" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<BranchDto>())!;
        Assert.Equal(branch with { Name = "Kho quận 1", Type = "Warehouse", Address = null }, updated);
        Assert.Equal([updated], await owner.ListBranchesAsync());
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-04 AC-3")]
    public async Task SetBranchActive_OffThenOn_ListFiltersByState()
    {
        var owner = await factory.LoginAsOwnerAsync(await _anonymous.RegisterTenantAsync());
        var open = await owner.CreateBranchAsync("CH-01");
        var closing = await owner.CreateBranchAsync("CH-02");

        var off = await owner.PatchAsJsonAsync($"/branches/{closing.Id}/active", new { isActive = false });

        Assert.Equal(closing with { IsActive = false }, await off.Content.ReadFromJsonAsync<BranchDto>());
        Assert.Equal([open], await owner.ListBranchesAsync("?isActive=true"));
        Assert.Equal([closing with { IsActive = false }], await owner.ListBranchesAsync("?isActive=false"));
        Assert.Equal(2, (await owner.ListBranchesAsync()).Count);

        var on = await owner.PatchAsJsonAsync($"/branches/{closing.Id}/active", new { isActive = true });

        Assert.Equal(closing, await on.Content.ReadFromJsonAsync<BranchDto>());
    }

    // Điều kiện xong của W1-05, phía phát: mọi thay đổi ghi BranchUpserted vào outbox với trạng thái đầy đủ.
    // Phía nhận kiểm ở Oism.Core.IntegrationTests/Messaging/ReferenceConsumerTests.
    [Fact]
    [Trait("UseCase", "UC-AUTH-04 AC-4")]
    public async Task Branch_EveryChange_EnqueuesBranchUpsertedWithFullStateAndNextVersion()
    {
        var tenant = await _anonymous.RegisterTenantAsync();
        var owner = await factory.LoginAsOwnerAsync(tenant);

        var branch = await owner.CreateBranchAsync("CH-01", name: "Cửa hàng 1");
        await owner.PutAsJsonAsync($"/branches/{branch.Id}", new { name = "Cửa hàng một", type = "Warehouse" });
        await owner.PatchAsJsonAsync($"/branches/{branch.Id}/active", new { isActive = false });
        // Lần sửa bị từ chối không được để lại thông điệp.
        await owner.PutAsJsonAsync($"/branches/{branch.Id}", new { name = "", type = "Store" });

        Assert.Equal(
            [
                new BranchUpserted(branch.Id, "CH-01", "Cửa hàng 1", "Store", IsActive: true, Version: 1),
                new BranchUpserted(branch.Id, "CH-01", "Cửa hàng một", "Warehouse", IsActive: true, Version: 2),
                new BranchUpserted(branch.Id, "CH-01", "Cửa hàng một", "Warehouse", IsActive: false, Version: 3),
            ],
            await factory.BranchEventsAsync(tenant.TenantId));
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-04 AC-1")]
    public async Task Branch_InvalidInputOrUnknownId_Returns400Or404AndEnqueuesNothing()
    {
        var tenant = await _anonymous.RegisterTenantAsync();
        var owner = await factory.LoginAsOwnerAsync(tenant);

        var blankCode = await owner.PostAsJsonAsync("/branches", new { code = " ", name = "Không mã", type = "Store" });
        var unknownType = await owner.PostAsJsonAsync("/branches", new { code = "CH-01", name = "Sai loại", type = "Kiosk" });
        var unknownUpdate = await owner.PutAsJsonAsync($"/branches/{Guid.NewGuid()}", new { name = "Không có", type = "Store" });
        var unknownToggle = await owner.PatchAsJsonAsync($"/branches/{Guid.NewGuid()}/active", new { isActive = false });

        var problem = await blankCode.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("code", out _), problem.ToString());
        await unknownType.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await unknownUpdate.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await unknownToggle.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        Assert.Empty(await owner.ListBranchesAsync());
        Assert.Empty(await factory.BranchEventsAsync(tenant.TenantId));
    }

    // Bảng quyền ở docs/architecture/security.md: Staff chỉ xem danh sách, Cashier không vào được.
    [Fact]
    [Trait("UseCase", "UC-AUTH-03 AC-2")]
    [Trait("UseCase", "UC-AUTH-03 AC-3")]
    public async Task Branches_StaffMayOnlyList_CashierIsForbidden()
    {
        var owner = await factory.LoginAsOwnerAsync(await _anonymous.RegisterTenantAsync());
        var branch = await owner.CreateBranchAsync("CH-01");
        var (staffEmail, cashierEmail) = (ApiExtensions.NewEmail(), ApiExtensions.NewEmail());
        await owner.CreateUserAsync("Staff", staffEmail);
        await owner.CreateUserAsync("Cashier", cashierEmail, branch.Id);
        var staff = factory.Authorized((await _anonymous.LoginAsync(staffEmail)).AccessToken);
        var cashier = factory.Authorized((await _anonymous.LoginAsync(cashierEmail)).AccessToken);

        Assert.Equal([branch], await staff.ListBranchesAsync());
        var create = await staff.PostAsJsonAsync("/branches", new { code = "CH-02", name = "Bị chặn", type = "Store" });
        var update = await staff.PutAsJsonAsync($"/branches/{branch.Id}", new { name = "Bị chặn", type = "Store" });
        var toggle = await staff.PatchAsJsonAsync($"/branches/{branch.Id}/active", new { isActive = false });
        var cashierList = await cashier.GetAsync("/branches");

        await create.AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        await update.AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        await toggle.AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        await cashierList.AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        Assert.Equal([branch], await owner.ListBranchesAsync());
    }
}
