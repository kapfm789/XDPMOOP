using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Oism.BuildingBlocks.Auth;
using Oism.Contracts;
using Oism.Core.Application;
using Oism.Core.Application.Inventory;
using static Oism.Core.IntegrationTests.TestKit;

namespace Oism.Core.IntegrationTests.Inventory;

// UC-INV-05 (FR-REP-03): đặt ngưỡng tồn tối thiểu cho một SKU tại một chi nhánh.
public sealed class SetThresholdTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private HttpClient Owner => factory.CreateClient(Roles.Owner, _tenantId);

    private HttpClient Staff => factory.CreateClient(Roles.Staff, _tenantId);

    [Fact]
    [Trait("UseCase", "UC-INV-05 AC-1")]
    [Trait("UseCase", "UC-INV-05 AC-2")]
    public async Task SetThreshold_ValueThenEmpty_SavesItAndEmitsStockChangedWithTheNewThreshold()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01", "Áo thun, Đen");
        await factory.PostLedgerAsync(_tenantId, branch, In(sku, 10));

        var set = await Owner.PutAsJsonAsync("/stock/threshold", new { branchId = branch, skuId = sku, threshold = 5 });

        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        Assert.Equal(
            new StockDto(branch, sku, "AO-01", "Áo thun, Đen", OnHand: 10, Reserved: 0, Available: 10, AvgCost: 100_000m, Threshold: 5),
            await set.Content.ReadFromJsonAsync<StockDto>());
        Assert.Equal(5, Assert.Single((await Staff.GetFromJsonAsync<PagedResult<StockDto>>("/stock"))!.Items).Threshold);

        // Để trống ngưỡng: SKU không còn sinh cảnh báo. Staff đặt được, nhưng phản hồi không mang giá vốn.
        var cleared = await Staff.PutAsJsonAsync("/stock/threshold", new { branchId = branch, skuId = sku });

        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        var row = await cleared.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, row.GetProperty("threshold").ValueKind);
        Assert.False(row.TryGetProperty("avgCost", out _));
        var balance = (await factory.BalanceAsync(_tenantId, branch, sku))!;
        Assert.Equal((null, 10, 0, 3L), (balance.ReorderThreshold, balance.OnHand, balance.Reserved, balance.Version));
        // Mỗi lần đặt phát một StockChanged mang ngưỡng và version mới; tồn và sổ không đổi.
        Assert.Equal(
            [
                new StockChanged(branch, sku, 10, 0, 10, 100_000m, null, 1),
                new StockChanged(branch, sku, 10, 0, 10, 100_000m, 5, 2),
                new StockChanged(branch, sku, 10, 0, 10, 100_000m, null, 3),
            ],
            (await factory.OutboxAsync<StockChanged>(_tenantId)).OrderBy(message => message.Version));
        Assert.Single(await factory.LedgerAsync(_tenantId));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    // SKU chưa từng có hàng ở chi nhánh vẫn đặt ngưỡng được: dòng số dư được tạo với tồn 0.
    [Fact]
    [Trait("UseCase", "UC-INV-05 AC-1")]
    public async Task SetThreshold_SkuWithoutBalanceAtTheBranch_CreatesTheBalanceAtZero()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");

        var response = await Staff.PutAsJsonAsync("/stock/threshold", new { branchId = branch, skuId = sku, threshold = 0 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var balance = (await factory.BalanceAsync(_tenantId, branch, sku))!;
        Assert.Equal((0, 0, 0, 1L), (balance.ReorderThreshold, balance.OnHand, balance.Reserved, balance.Version));
        Assert.Equal(new StockChanged(branch, sku, 0, 0, 0, 0m, 0, 1), Assert.Single(await factory.OutboxAsync<StockChanged>(_tenantId)));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("UseCase", "UC-INV-05 AC-3")]
    [Trait("UseCase", "UC-AUTH-03 AC-4")]
    public async Task SetThreshold_NegativeOrUnknownReferenceOrWrongRole_IsRefusedAndChangesNothing()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(sku, 10));
        var body = new { branchId = branch, skuId = sku, threshold = 5 };

        await (await Staff.PutAsJsonAsync("/stock/threshold", new { branchId = branch, skuId = sku, threshold = -1 }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await Staff.PutAsJsonAsync("/stock/threshold", new { skuId = sku, threshold = 5 }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await Staff.PutAsJsonAsync("/stock/threshold", new { branchId = Guid.NewGuid(), skuId = sku, threshold = 5 }))
            .AssertProblemAsync(HttpStatusCode.Conflict, "reference_not_ready");
        await (await Staff.PutAsJsonAsync("/stock/threshold", new { branchId = branch, skuId = Guid.NewGuid(), threshold = 5 }))
            .AssertProblemAsync(HttpStatusCode.Conflict, "reference_not_ready");
        await (await factory.CreateClient(Roles.Cashier, _tenantId).PutAsJsonAsync("/stock/threshold", body))
            .AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        await (await factory.CreateClient().PutAsJsonAsync("/stock/threshold", body))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthenticated");

        var balance = (await factory.BalanceAsync(_tenantId, branch, sku))!;
        Assert.Equal((null, 1L), (balance.ReorderThreshold, balance.Version));
        Assert.Single(await factory.OutboxAsync<StockChanged>(_tenantId));
    }

    // T14: tenant A không đặt được ngưỡng lên chi nhánh hay SKU của tenant B, và không dựng số dư nào từ ID của B.
    [Fact]
    [Trait("Scenario", "T14")]
    public async Task SetThreshold_BranchOrSkuOfAnotherTenant_IsRefusedAndItsBalanceIsUntouched()
    {
        var otherTenantId = Guid.NewGuid();
        var branchOfOther = await factory.SeedBranchAsync(otherTenantId);
        var skuOfOther = await factory.SeedSkuAsync(otherTenantId, "B-01");
        await factory.PostLedgerAsync(otherTenantId, branchOfOther, In(skuOfOther, 10));
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "A-01");

        var atBranchOfOther = await Owner.PutAsJsonAsync("/stock/threshold", new { branchId = branchOfOther, skuId = sku, threshold = 5 });
        var onSkuOfOther = await Owner.PutAsJsonAsync("/stock/threshold", new { branchId = branch, skuId = skuOfOther, threshold = 5 });
        var both = await Owner.PutAsJsonAsync("/stock/threshold", new { branchId = branchOfOther, skuId = skuOfOther, threshold = 5 });

        await atBranchOfOther.AssertProblemAsync(HttpStatusCode.Conflict, "reference_not_ready");
        await onSkuOfOther.AssertProblemAsync(HttpStatusCode.Conflict, "reference_not_ready");
        await both.AssertProblemAsync(HttpStatusCode.Conflict, "reference_not_ready");
        var balanceOfOther = (await factory.BalanceAsync(otherTenantId, branchOfOther, skuOfOther))!;
        Assert.Equal((null, 1L), (balanceOfOther.ReorderThreshold, balanceOfOther.Version));
        Assert.Equal(0, (await Owner.GetFromJsonAsync<PagedResult<StockDto>>("/stock"))!.Total);
        Assert.Empty(await factory.OutboxAsync<StockChanged>(_tenantId));
        await factory.AssertInventoryInvariantsAsync(otherTenantId);
    }
}
