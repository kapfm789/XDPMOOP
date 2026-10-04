using System.Net.Http.Json;
using Oism.BuildingBlocks.Auth;
using Oism.Core.Application;
using Oism.Core.Application.Inventory;
using static Oism.Core.IntegrationTests.TestKit;

namespace Oism.Core.IntegrationTests.Tenancy;

// T14 cho tồn và sổ: tenant A không đọc được và không đổi được số dư, sổ của tenant B.
public sealed class TenantIsolationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    [Trait("Scenario", "T14")]
    public async Task StockAndLedger_IdsOfAnotherTenant_ShowNothingAndLeaveItsBalanceUnchanged()
    {
        var (tenantA, tenantB, branchOfB) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var skuOfB = await factory.SeedSkuAsync(tenantB, "B-01");
        await factory.PostLedgerAsync(tenantB, branchOfB, In(skuOfB, 10));
        var ownerA = factory.CreateClient(Roles.Owner, tenantA);

        PagedResult<StockDto>[] stock =
        [
            (await ownerA.GetFromJsonAsync<PagedResult<StockDto>>("/stock"))!,
            (await ownerA.GetFromJsonAsync<PagedResult<StockDto>>($"/stock?branchId={branchOfB}&skuId={skuOfB}"))!,
        ];
        PagedResult<LedgerLineDto>[] ledger =
        [
            (await ownerA.GetFromJsonAsync<PagedResult<LedgerLineDto>>("/ledger"))!,
            (await ownerA.GetFromJsonAsync<PagedResult<LedgerLineDto>>($"/ledger?branchId={branchOfB}&skuId={skuOfB}"))!,
        ];
        // Ghi sổ ở tenant A với đúng chi nhánh và SKU của B: tạo số dư riêng của A, không chạm dòng của B.
        await factory.PostLedgerAsync(tenantA, branchOfB, In(skuOfB, 3));

        Assert.All(stock, page => Assert.Equal((0, 0), (page.Total, page.Items.Count)));
        Assert.All(ledger, page => Assert.Equal((0, 0), (page.Total, page.Items.Count)));
        var balanceOfB = (await factory.BalanceAsync(tenantB, branchOfB, skuOfB))!;
        Assert.Equal((tenantB, 10, 1L), (balanceOfB.TenantId, balanceOfB.OnHand, balanceOfB.Version));
        Assert.Equal(10, Assert.Single(await factory.LedgerAsync(tenantB)).Quantity);
        Assert.Equal((tenantA, 3), ((await factory.BalanceAsync(tenantA, branchOfB, skuOfB))!.TenantId, Assert.Single(await factory.LedgerAsync(tenantA)).Quantity));
        await factory.AssertInventoryInvariantsAsync(tenantA);
        await factory.AssertInventoryInvariantsAsync(tenantB);
    }
}
