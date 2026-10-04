using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Oism.BuildingBlocks.Auth;
using Oism.Core.Application;
using Oism.Core.Application.Inventory;
using Oism.Core.Domain.Inventory;
using static Oism.Core.IntegrationTests.TestKit;

namespace Oism.Core.IntegrationTests.Inventory;

// UC-INV-01: xem tồn và sổ giao dịch qua API.
public sealed class StockAndLedgerTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private HttpClient Owner => factory.CreateClient(Roles.Owner, _tenantId);

    private HttpClient Staff => factory.CreateClient(Roles.Staff, _tenantId);

    [Fact]
    [Trait("UseCase", "UC-INV-01 AC-1")]
    public async Task ListStock_SkusWithStock_ShowsOnHandReservedAvailableAndCostOnlyToOwner()
    {
        var (branch, otherBranch) = (Guid.NewGuid(), Guid.NewGuid());
        var shirt = await factory.SeedSkuAsync(_tenantId, "AO-01", "Áo thun, Đen");
        var trousers = await factory.SeedSkuAsync(_tenantId, "QUAN-01", "Quần kaki");
        await factory.PostLedgerAsync(_tenantId, branch, In(shirt, 10), In(trousers, 4), Out(trousers, 1));
        await factory.PostLedgerAsync(_tenantId, otherBranch, In(shirt, 7));

        var atBranch = (await Owner.GetFromJsonAsync<PagedResult<StockDto>>($"/stock?branchId={branch}"))!;
        var ofStaff = (await Staff.GetFromJsonAsync<JsonElement>($"/stock?branchId={branch}")).GetProperty("items");
        var ofOwner = (await Owner.GetFromJsonAsync<JsonElement>($"/stock?branchId={branch}")).GetProperty("items");
        var bySku = (await Staff.GetFromJsonAsync<PagedResult<StockDto>>($"/stock?skuId={shirt}"))!;
        var byQuery = (await Staff.GetFromJsonAsync<PagedResult<StockDto>>("/stock?query=quần"))!;
        var secondPage = (await Staff.GetFromJsonAsync<PagedResult<StockDto>>("/stock?page=2&pageSize=2"))!;

        // Xếp theo mã SKU; available = on_hand - reserved.
        Assert.Equal(
            [(shirt, "AO-01", "Áo thun, Đen", 10, 0, 10), (trousers, "QUAN-01", "Quần kaki", 3, 0, 3)],
            atBranch.Items.Select(row => (row.SkuId, row.SkuCode, row.Name, row.OnHand, row.Reserved, row.Available)));
        Assert.All(atBranch.Items, row => Assert.Equal(branch, row.BranchId));
        Assert.Equal((1, 20, 2), (atBranch.Page, atBranch.PageSize, atBranch.Total));
        // Giá vốn chỉ trả cho Owner; với Staff trường này không có trong phản hồi.
        Assert.All(ofOwner.EnumerateArray(), row => Assert.True(row.TryGetProperty("avgCost", out _)));
        Assert.All(ofStaff.EnumerateArray(), row => Assert.False(row.TryGetProperty("avgCost", out _)));
        Assert.Equal([10, 7], bySku.Items.Select(row => row.OnHand).OrderDescending());
        Assert.Equal(trousers, Assert.Single(byQuery.Items).SkuId);
        Assert.Equal((3, 1), (secondPage.Total, secondPage.Items.Count));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("UseCase", "UC-INV-01 AC-2")]
    public async Task ListLedger_FilteredByBranchSkuAndTime_ReturnsLinesInWriteOrderWithCostOnlyToOwner()
    {
        var (branch, otherBranch) = (Guid.NewGuid(), Guid.NewGuid());
        var shirt = await factory.SeedSkuAsync(_tenantId, "AO-01", "Áo thun, Đen");
        var trousers = await factory.SeedSkuAsync(_tenantId, "QUAN-01", "Quần kaki");
        var first = Assert.Single(await factory.PostLedgerAsync(_tenantId, branch, In(shirt, 10, unitCost: 100_000)));
        var second = Assert.Single(await factory.PostLedgerAsync(_tenantId, branch, Out(shirt, 4, unitCost: 100_000, LedgerReason.Sale)));
        await factory.PostLedgerAsync(_tenantId, branch, In(trousers, 2));
        await factory.PostLedgerAsync(_tenantId, otherBranch, In(shirt, 1));
        Assert.True(first.CreatedAt < second.CreatedAt);

        var filter = $"/ledger?branchId={branch}&skuId={shirt}";
        var lines = (await Owner.GetFromJsonAsync<PagedResult<LedgerLineDto>>(filter))!;
        var ofStaff = (await Staff.GetFromJsonAsync<JsonElement>(filter)).GetProperty("items");
        var fromSecond = (await Staff.GetFromJsonAsync<PagedResult<LedgerLineDto>>($"{filter}&from={Timestamp(second.CreatedAt)}"))!;
        var untilFirst = (await Staff.GetFromJsonAsync<PagedResult<LedgerLineDto>>($"{filter}&to={Timestamp(first.CreatedAt)}"))!;
        var everything = (await Staff.GetFromJsonAsync<PagedResult<LedgerLineDto>>("/ledger?pageSize=3"))!;

        // Thứ tự ghi; mỗi dòng có loại, lý do, số lượng, số dư sau giao dịch, chứng từ và người tạo.
        Assert.Equal(
            [("IN", "Purchase", 10, 10, (decimal?)100_000m), ("OUT", "Sale", 4, 6, 100_000m)],
            lines.Items.Select(line => (line.Type, line.Reason, line.Quantity, line.BalanceAfter, line.UnitCost)));
        Assert.Equal(
            (first.Id, first.Seq, "AO-01", "Áo thun, Đen", "PurchaseReceipt", first.ReferenceId, first.CreatedBy, first.CreatedAt),
            (lines.Items[0].Id, lines.Items[0].Seq, lines.Items[0].SkuCode, lines.Items[0].Name, lines.Items[0].ReferenceType,
                lines.Items[0].ReferenceId, lines.Items[0].CreatedBy, lines.Items[0].CreatedAt));
        Assert.All(ofStaff.EnumerateArray(), line => Assert.False(line.TryGetProperty("unitCost", out _)));
        Assert.Equal(second.Id, Assert.Single(fromSecond.Items).Id);
        Assert.Equal(first.Id, Assert.Single(untilFirst.Items).Id);
        Assert.Equal((4, 3), (everything.Total, everything.Items.Count));
        Assert.Equal(everything.Items.Select(line => line.Seq).Order(), everything.Items.Select(line => line.Seq));
        await factory.AssertInventoryInvariantsAsync(_tenantId);

        static string Timestamp(DateTimeOffset value) => Uri.EscapeDataString(value.ToString("O"));
    }

    // UC-INV-01 AC-4: không có API nào sửa hoặc xóa dòng sổ.
    [Fact]
    [Trait("UseCase", "UC-INV-01 AC-4")]
    public async Task Ledger_WriteRequests_HaveNoEndpoint()
    {
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        var line = Assert.Single(await factory.PostLedgerAsync(_tenantId, Guid.NewGuid(), In(sku, 10)));

        HttpResponseMessage[] responses =
        [
            await Owner.PostAsJsonAsync("/ledger", new { quantity = 1 }),
            await Owner.PutAsJsonAsync($"/ledger/{line.Id}", new { quantity = 999 }),
            await Owner.DeleteAsync($"/ledger/{line.Id}"),
            await Owner.PutAsJsonAsync($"/ledger/{line.Seq}", new { quantity = 999 }),
            await Owner.DeleteAsync($"/ledger/{line.Seq}"),
        ];

        // Không route nào khớp: pipeline từ chối theo policy mặc định (403), hoặc trả 404, 405.
        Assert.All(responses, response => Assert.Contains(
            response.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed }));
        Assert.Equal(10, Assert.Single(await factory.LedgerAsync(_tenantId)).Quantity);
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-03 AC-4")]
    public async Task StockAndLedger_CashierOrNoToken_AreRefusedAndBadPagingReturns400()
    {
        var cashier = factory.CreateClient(Roles.Cashier, _tenantId);

        await (await cashier.GetAsync("/stock")).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        await (await cashier.GetAsync("/ledger")).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        await (await factory.CreateClient().GetAsync("/stock")).AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthenticated");
        await (await factory.CreateClient().GetAsync("/ledger")).AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthenticated");
        await (await Staff.GetAsync("/stock?page=0")).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await Staff.GetAsync("/ledger?pageSize=101")).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
    }
}
