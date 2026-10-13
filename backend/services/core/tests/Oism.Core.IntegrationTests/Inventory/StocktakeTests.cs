using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Auth;
using Oism.Contracts;
using Oism.Core.Application;
using Oism.Core.Application.Inventory;
using Oism.Core.Domain.Inventory;
using static Oism.Core.IntegrationTests.TestKit;

namespace Oism.Core.IntegrationTests.Inventory;

// W3-06: kiểm kê, bút toán điều chỉnh, chặn khi số đếm nhỏ hơn lượng đang giữ (UC-INV-04, FR-INV-04).
public sealed class StocktakeTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private HttpClient Staff => factory.CreateClient(Roles.Staff, _tenantId);

    [Fact]
    [Trait("UseCase", "UC-INV-04 AC-1")]
    [Trait("UseCase", "UC-INV-04 AC-2")]
    [Trait("UseCase", "UC-INV-04 AC-3")]
    [Trait("UseCase", "UC-INV-04 AC-6")]
    public async Task Stocktake_CountThenPost_AdjustsOnHandToTheCountWithOneLedgerLinePerDifferenceAndKeepsTheAverageCost()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var shirt = await factory.SeedSkuAsync(_tenantId, "AO-01", "Áo thun, Đen");
        var trousers = await factory.SeedSkuAsync(_tenantId, "QUAN-01");
        var hat = await factory.SeedSkuAsync(_tenantId, "MU-01");
        // SKU chưa từng có ở chi nhánh.
        var scarf = await factory.SeedSkuAsync(_tenantId, "KHAN-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(shirt, 10, 100_000), In(trousers, 10, 80_000), In(hat, 5, 50_000));

        var opened = await SendAsync<StocktakeDto>(Staff.PostAsJsonAsync("/stocktakes", new { branchId = branch }), HttpStatusCode.Created);
        Assert.Equal((branch, "Open", (DateTimeOffset?)null), (opened.BranchId, opened.Status, opened.PostedAt));
        Assert.NotNull(opened.CreatedBy);
        Assert.Empty(opened.Items);

        // Nhập lại số đếm là thay cả bộ dòng của phiên.
        await CountAsync(opened.Id, new { skuId = shirt, countedQty = 99 });
        var counted = await CountAsync(
            opened.Id,
            new { skuId = shirt, countedQty = 7 }, new { skuId = trousers, countedQty = 12 },
            new { skuId = hat, countedQty = 5 }, new { skuId = scarf, countedQty = 3 });

        // AC-1: phiên còn Open thì tồn và sổ chưa bị tác động.
        Assert.Equal("Open", counted.Status);
        Assert.Equal(
            [("AO-01", "Áo thun, Đen", 7), ("KHAN-01", "Áo thun", 3), ("MU-01", "Áo thun", 5), ("QUAN-01", "Áo thun", 12)],
            counted.Items.Select(item => (item.SkuCode, item.SkuName, item.CountedQty)));
        Assert.All(counted.Items, item => Assert.Equal(((int?)null, (int?)null), (item.SystemQty, item.Difference)));
        Assert.Equal((10, 0, 10), await factory.StockAsync(_tenantId, branch, shirt));
        Assert.Null(await factory.BalanceAsync(_tenantId, branch, scarf));
        Assert.Equal(3, (await factory.LedgerAsync(_tenantId)).Count);
        // Một áo rời kho giữa lúc đếm và lúc chốt: số trên sổ là số đọc lúc chốt.
        await factory.PostLedgerAsync(_tenantId, branch, Out(shirt, 1, reason: LedgerReason.Sale));

        var posted = await SendAsync<StocktakeDto>(Staff.PostAsync($"/stocktakes/{opened.Id}/post", null), HttpStatusCode.OK);

        Assert.Equal("Posted", posted.Status);
        Assert.NotNull(posted.PostedAt);
        Assert.Equal(
            [(shirt, 7, 9, -2), (scarf, 3, 0, 3), (hat, 5, 5, 0), (trousers, 12, 10, 2)],
            posted.Items.Select(item => (item.SkuId, item.CountedQty, item.SystemQty!.Value, item.Difference!.Value)));
        // AC-2: on_hand bằng số đếm; AC-6: giá vốn bình quân không đổi, SKU mới có giá vốn 0 tới lần nhập mua đầu tiên.
        Assert.Equal((7, 0, 7), await factory.StockAsync(_tenantId, branch, shirt));
        Assert.Equal((12, 0, 12), await factory.StockAsync(_tenantId, branch, trousers));
        Assert.Equal((5, 0, 5), await factory.StockAsync(_tenantId, branch, hat));
        Assert.Equal((3, 0, 3), await factory.StockAsync(_tenantId, branch, scarf));
        Assert.Equal(100_000m, (await factory.BalanceAsync(_tenantId, branch, shirt))!.AvgCost);
        Assert.Equal(80_000m, (await factory.BalanceAsync(_tenantId, branch, trousers))!.AvgCost);
        Assert.Equal(0m, (await factory.BalanceAsync(_tenantId, branch, scarf))!.AvgCost);
        // Mỗi chênh lệch một dòng sổ lý do StocktakeAdjust với đơn giá là giá vốn hiện tại; AC-3: SKU đếm bằng sổ không có dòng nào.
        var adjustments = (await factory.LedgerAsync(_tenantId)).Where(line => line.ReferenceType == LedgerReference.Stocktake).ToList();
        Assert.All(adjustments, line =>
        {
            Assert.Equal((LedgerReason.StocktakeAdjust, opened.Id, branch), (line.Reason, line.ReferenceId, line.BranchId));
            Assert.NotNull(line.CreatedBy);
        });
        Assert.Equal(
            new[] { (shirt, LedgerType.OUT, 2, 7, 100_000m), (trousers, LedgerType.IN, 2, 12, 80_000m), (scarf, LedgerType.IN, 3, 3, 0m) }.ToHashSet(),
            adjustments.Select(line => (line.SkuId, line.Type, line.Quantity, line.BalanceAfter, line.UnitCost)).ToHashSet());
        Assert.Equal(3, adjustments.Count);
        var changes = await factory.OutboxAsync<StockChanged>(_tenantId);
        Assert.Contains(changes, change => (change.SkuId, change.OnHand, change.AvgCost) == (shirt, 7, 100_000m));
        Assert.Contains(changes, change => (change.SkuId, change.OnHand) == (scarf, 3));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("Scenario", "T13")]
    [Trait("UseCase", "UC-INV-04 AC-4")]
    public async Task PostStocktake_CountBelowTheHeldQuantity_Returns409WithTheHoldingOrdersAndAdjustsNoSku()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var shirt = await factory.SeedSkuAsync(_tenantId, "AO-01");
        var trousers = await factory.SeedSkuAsync(_tenantId, "QUAN-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(shirt, 10), In(trousers, 10));
        // reserved = 8 trên hai đơn; đơn thứ ba giữ SKU khác nên không nằm trong danh sách.
        var first = await Staff.CreateOrderAsync(branch, new { skuId = shirt, quantity = 5 });
        var second = await Staff.CreateOrderAsync(branch, new { skuId = shirt, quantity = 2 }, new { skuId = shirt, quantity = 1 });
        await Staff.CreateOrderAsync(branch, new { skuId = trousers, quantity = 1 });
        var stocktake = await SendAsync<StocktakeDto>(Staff.PostAsJsonAsync("/stocktakes", new { branchId = branch }), HttpStatusCode.Created);
        await CountAsync(stocktake.Id, new { skuId = shirt, countedQty = 6 }, new { skuId = trousers, countedQty = 4 });
        var ledgerBefore = (await factory.LedgerAsync(_tenantId)).Count;
        var outboxBefore = await factory.OutboxRowCountAsync(_tenantId);

        var response = await Staff.PostAsync($"/stocktakes/{stocktake.Id}/post", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("stocktake_below_reserved", problem.GetProperty("code").GetString());
        var shortfall = Assert.Single(problem.GetProperty("details").EnumerateArray());
        Assert.Equal(
            (shirt, 6, 8),
            (shortfall.GetProperty("skuId").GetGuid(), shortfall.GetProperty("countedQty").GetInt32(), shortfall.GetProperty("reserved").GetInt32()));
        Assert.Equal(
            [(first.Id, first.OrderNumber, "Admin", 5), (second.Id, second.OrderNumber, "Admin", 3)],
            shortfall.GetProperty("orders").EnumerateArray().Select(order => (
                order.GetProperty("orderId").GetGuid(), order.GetProperty("orderNumber").GetString()!,
                order.GetProperty("channel").GetString()!, order.GetProperty("quantity").GetInt32())));
        // Không SKU nào của phiên bị điều chỉnh, kể cả SKU hợp lệ; phiên vẫn Open.
        Assert.Equal((10, 8, 2), await factory.StockAsync(_tenantId, branch, shirt));
        Assert.Equal((10, 1, 9), await factory.StockAsync(_tenantId, branch, trousers));
        Assert.Equal(ledgerBefore, (await factory.LedgerAsync(_tenantId)).Count);
        Assert.Equal(outboxBefore, await factory.OutboxRowCountAsync(_tenantId));
        var stored = await StoredAsync(stocktake.Id);
        Assert.Equal((StocktakeStatus.Open, (DateTimeOffset?)null), (stored.Status, stored.PostedAt));
        Assert.All(stored.Items, item => Assert.Null(item.SystemQty));

        // Nhân viên hủy một đơn trong danh sách rồi chốt lại: reserved = 5, đếm 6 là hợp lệ.
        await Staff.PostAsync($"/orders/{second.Id}/cancel", null);
        var posted = await SendAsync<StocktakeDto>(Staff.PostAsync($"/stocktakes/{stocktake.Id}/post", null), HttpStatusCode.OK);

        Assert.Equal("Posted", posted.Status);
        Assert.Equal((6, 5, 1), await factory.StockAsync(_tenantId, branch, shirt));
        Assert.Equal((4, 1, 3), await factory.StockAsync(_tenantId, branch, trousers));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("UseCase", "UC-INV-04 AC-5")]
    public async Task PostStocktake_Twice_AdjustsOnceAndAPostedSessionTakesNoMoreCounts()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(sku, 10));
        var stocktake = await SendAsync<StocktakeDto>(Staff.PostAsJsonAsync("/stocktakes", new { branchId = branch }), HttpStatusCode.Created);
        await CountAsync(stocktake.Id, new { skuId = sku, countedQty = 8 });

        var first = await SendAsync<StocktakeDto>(Staff.PostAsync($"/stocktakes/{stocktake.Id}/post", null), HttpStatusCode.OK);
        var ledgerAfterFirst = (await factory.LedgerAsync(_tenantId)).Count;
        var outboxAfterFirst = await factory.OutboxRowCountAsync(_tenantId);
        var versionAfterFirst = (await factory.BalanceAsync(_tenantId, branch, sku))!.Version;
        var second = await SendAsync<StocktakeDto>(Staff.PostAsync($"/stocktakes/{stocktake.Id}/post", null), HttpStatusCode.OK);
        var recount = await Staff.PutAsJsonAsync($"/stocktakes/{stocktake.Id}/counts", new { items = new[] { new { skuId = sku, countedQty = 1 } } });

        Assert.Equal((first.Id, "Posted", first.PostedAt), (second.Id, second.Status, second.PostedAt));
        var line = Assert.Single(second.Items);
        Assert.Equal((sku, 8, (int?)10, (int?)-2), (line.SkuId, line.CountedQty, line.SystemQty, line.Difference));
        await recount.AssertProblemAsync(HttpStatusCode.Conflict, "invalid_state_transition");
        Assert.Equal((8, 0, 8), await factory.StockAsync(_tenantId, branch, sku));
        Assert.Equal(ledgerAfterFirst, (await factory.LedgerAsync(_tenantId)).Count);
        Assert.Equal(outboxAfterFirst, await factory.OutboxRowCountAsync(_tenantId));
        Assert.Equal(versionAfterFirst, (await factory.BalanceAsync(_tenantId, branch, sku))!.Version);
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("UseCase", "UC-INV-04 AC-1")]
    public async Task Stocktake_BadInputUnknownReferencesOrNoCounts_AreRefusedAndAnEmptySessionPostsWithoutTouchingStock()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var closedBranch = await factory.SeedBranchAsync(_tenantId, isActive: false);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(sku, 10));
        var stocktake = await SendAsync<StocktakeDto>(Staff.PostAsJsonAsync("/stocktakes", new { branchId = branch }), HttpStatusCode.Created);
        var counts = $"/stocktakes/{stocktake.Id}/counts";

        await (await Staff.PostAsJsonAsync("/stocktakes", new { branchId = Guid.NewGuid() }))
            .AssertProblemAsync(HttpStatusCode.Conflict, "reference_not_ready");
        await (await Staff.PostAsJsonAsync("/stocktakes", new { branchId = closedBranch }))
            .AssertProblemAsync(HttpStatusCode.Conflict, "inactive_reference");
        await (await Staff.PutAsJsonAsync(counts, new { items = Array.Empty<object>() }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await Staff.PutAsJsonAsync(counts, new { items = new[] { new { skuId = sku, countedQty = -1 } } }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await Staff.PutAsJsonAsync(counts, new { items = new[] { new { skuId = sku, countedQty = 1 }, new { skuId = sku, countedQty = 2 } } }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await Staff.PutAsJsonAsync(counts, new { items = new[] { new { skuId = Guid.NewGuid(), countedQty = 1 } } }))
            .AssertProblemAsync(HttpStatusCode.Conflict, "reference_not_ready");
        await (await Staff.PutAsJsonAsync($"/stocktakes/{Guid.NewGuid()}/counts", new { items = new[] { new { skuId = sku, countedQty = 1 } } }))
            .AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await Staff.PostAsync($"/stocktakes/{Guid.NewGuid()}/post", null)).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");

        // Không lần nào ở trên để lại số đếm: phiên chốt được mà không SKU nào bị đụng tới.
        var posted = await SendAsync<StocktakeDto>(Staff.PostAsync($"/stocktakes/{stocktake.Id}/post", null), HttpStatusCode.OK);

        Assert.Equal("Posted", posted.Status);
        Assert.Empty(posted.Items);
        Assert.Equal((10, 0, 10), await factory.StockAsync(_tenantId, branch, sku));
        Assert.Single(await factory.LedgerAsync(_tenantId));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("Scenario", "T14")]
    public async Task Stocktake_CashierOrAnotherTenant_CannotOpenCountOrPost()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(sku, 10));
        var stocktake = await SendAsync<StocktakeDto>(Staff.PostAsJsonAsync("/stocktakes", new { branchId = branch }), HttpStatusCode.Created);
        await CountAsync(stocktake.Id, new { skuId = sku, countedQty = 4 });
        var cashier = factory.CreateClient(Roles.Cashier, _tenantId, branch);
        var otherTenant = Guid.NewGuid();
        var outsider = factory.CreateClient(Roles.Owner, otherTenant);
        var skuOfOutsider = await factory.SeedSkuAsync(otherTenant, "AO-01");
        var body = new { items = new[] { new { skuId = skuOfOutsider, countedQty = 0 } } };

        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.PostAsJsonAsync("/stocktakes", new { branchId = branch })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.PutAsJsonAsync($"/stocktakes/{stocktake.Id}/counts", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.PostAsync($"/stocktakes/{stocktake.Id}/post", null)).StatusCode);
        // Chi nhánh của tenant khác không có bản sao ở tenant này (docs/architecture/multi-tenancy.md quy tắc 3).
        await (await outsider.PostAsJsonAsync("/stocktakes", new { branchId = branch })).AssertProblemAsync(HttpStatusCode.Conflict, "reference_not_ready");
        await (await outsider.PutAsJsonAsync($"/stocktakes/{stocktake.Id}/counts", body)).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await outsider.PostAsync($"/stocktakes/{stocktake.Id}/post", null)).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");

        var stored = await StoredAsync(stocktake.Id);
        Assert.Equal((StocktakeStatus.Open, sku, 4), (stored.Status, Assert.Single(stored.Items).SkuId, stored.Items.Single().CountedQty));
        Assert.Equal((10, 0, 10), await factory.StockAsync(_tenantId, branch, sku));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("Scenario", "T14")]
    [Trait("UseCase", "UC-INV-04 AC-1")]
    public async Task ListStocktakes_ByBranchAndStatus_ShowsOpenSessionsFirstWithTheirLinesAndNothingOfAnotherTenant()
    {
        var (first, second) = (await factory.SeedBranchAsync(_tenantId), await factory.SeedBranchAsync(_tenantId));
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        await factory.PostLedgerAsync(_tenantId, first, In(sku, 10));
        var posted = await SendAsync<StocktakeDto>(Staff.PostAsJsonAsync("/stocktakes", new { branchId = first }), HttpStatusCode.Created);
        await CountAsync(posted.Id, new { skuId = sku, countedQty = 4 });
        await Staff.PostAsync($"/stocktakes/{posted.Id}/post", null);
        var open = await SendAsync<StocktakeDto>(Staff.PostAsJsonAsync("/stocktakes", new { branchId = first }), HttpStatusCode.Created);
        await CountAsync(open.Id, new { skuId = sku, countedQty = 7 });
        var elsewhere = await SendAsync<StocktakeDto>(Staff.PostAsJsonAsync("/stocktakes", new { branchId = second }), HttpStatusCode.Created);
        var otherTenant = Guid.NewGuid();
        var outsider = factory.CreateClient(Roles.Owner, otherTenant);
        var ofOutsider = await SendAsync<StocktakeDto>(
            outsider.PostAsJsonAsync("/stocktakes", new { branchId = await factory.SeedBranchAsync(otherTenant) }), HttpStatusCode.Created);

        var all = (await Staff.GetFromJsonAsync<PagedResult<StocktakeDto>>("/stocktakes"))!;
        var ofBranch = (await Staff.GetFromJsonAsync<PagedResult<StocktakeDto>>($"/stocktakes?branchId={first}"))!;
        var postedOnly = (await Staff.GetFromJsonAsync<PagedResult<StocktakeDto>>($"/stocktakes?status=Posted&branchId={first}"))!;
        var secondPage = (await Staff.GetFromJsonAsync<PagedResult<StocktakeDto>>($"/stocktakes?branchId={first}&page=2&pageSize=1"))!;

        // Phiên còn Open đứng trước, rồi tới phiên đã chốt; mỗi phiên kèm các dòng của nó.
        Assert.Equal((3, 3), (all.Total, all.Items.Count));
        Assert.Equal(new[] { open.Id, elsewhere.Id }.ToHashSet(), all.Items.Take(2).Select(item => item.Id).ToHashSet());
        Assert.Equal(posted.Id, all.Items[2].Id);
        Assert.Equal([open.Id, posted.Id], ofBranch.Items.Select(item => item.Id));
        Assert.Equal(("Open", "AO-01", 7, (int?)null), Line(ofBranch.Items[0]));
        Assert.Equal(("Posted", "AO-01", 4, (int?)-6), Line(Assert.Single(postedOnly.Items)));
        Assert.Equal((2, 2, 1, posted.Id), (secondPage.Total, secondPage.Page, secondPage.PageSize, Assert.Single(secondPage.Items).Id));
        await (await Staff.GetAsync("/stocktakes?status=Closed")).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await Staff.GetAsync("/stocktakes?pageSize=0")).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.CreateClient(Roles.Cashier, _tenantId, first).GetAsync("/stocktakes")).StatusCode);
        // Tenant khác chỉ thấy phiên của mình, kể cả khi lọc theo chi nhánh của tenant này.
        Assert.Equal(ofOutsider.Id, Assert.Single((await outsider.GetFromJsonAsync<PagedResult<StocktakeDto>>("/stocktakes"))!.Items).Id);
        Assert.Empty((await outsider.GetFromJsonAsync<PagedResult<StocktakeDto>>($"/stocktakes?branchId={first}"))!.Items);
        await factory.AssertInventoryInvariantsAsync(_tenantId);

        static (string Status, string SkuCode, int CountedQty, int? Difference) Line(StocktakeDto stocktake) =>
            (stocktake.Status, stocktake.Items.Single().SkuCode, stocktake.Items.Single().CountedQty, stocktake.Items.Single().Difference);
    }

    private Task<StocktakeDto> CountAsync(Guid stocktakeId, params object[] items) =>
        SendAsync<StocktakeDto>(Staff.PutAsJsonAsync($"/stocktakes/{stocktakeId}/counts", new { items }), HttpStatusCode.OK);

    private static async Task<T> SendAsync<T>(Task<HttpResponseMessage> request, HttpStatusCode expected)
    {
        var response = await request;
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private Task<Stocktake> StoredAsync(Guid id) =>
        factory.QueryAsync(_tenantId, db => db.Stocktakes.AsNoTracking().Include(stocktake => stocktake.Items).SingleAsync(stocktake => stocktake.Id == id));
}
