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

// W3-05: chuyển kho hai bước, mang giá vốn nơi gửi sang nơi nhận (UC-INV-03, FR-INV-03).
public sealed class StockTransferTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private HttpClient Staff => factory.CreateClient(Roles.Staff, _tenantId);

    private HttpClient Owner => factory.CreateClient(Roles.Owner, _tenantId);

    [Fact]
    [Trait("Scenario", "T12")]
    [Trait("UseCase", "UC-INV-03 AC-1")]
    [Trait("UseCase", "UC-INV-03 AC-3")]
    [Trait("UseCase", "UC-INV-03 AC-4")]
    [Trait("UseCase", "UC-INV-03 AC-5")]
    public async Task Transfer_ShipThenReceive_GoodsAreInTransitBetweenTheTwoStepsAndCarryTheSenderCost()
    {
        var (from, to) = (await factory.SeedBranchAsync(_tenantId), await factory.SeedBranchAsync(_tenantId));
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01", "Áo thun, Đen");
        await factory.PostLedgerAsync(_tenantId, from, In(sku, 10, 100_000));
        await factory.PostLedgerAsync(_tenantId, to, In(sku, 6, 130_000));

        var draft = await CreateAsync(Owner, from, to, new { skuId = sku, quantity = 4 });
        Assert.Equal(("Draft", from, to, (DateTimeOffset?)null), (draft.Status, draft.FromBranchId, draft.ToBranchId, draft.ShippedAt));
        Assert.StartsWith("CK", draft.TransferNumber);
        Assert.NotNull(draft.CreatedBy);
        Assert.Equal((sku, "AO-01", "Áo thun, Đen", 4, (decimal?)null), Line(Assert.Single(draft.Items)));
        // Phiếu còn Draft thì tồn chưa bị tác động.
        Assert.Equal((10, 0, 10), await factory.StockAsync(_tenantId, from, sku));

        var shipped = await PostAsync<TransferDto>(Owner, $"/transfers/{draft.Id}/ship");

        // AC-1: on_hand nơi gửi giảm, giá vốn nơi gửi không đổi, sổ có dòng OUT lý do TransferOut với đơn giá là giá vốn nơi gửi.
        Assert.Equal("InTransit", shipped.Status);
        Assert.NotNull(shipped.ShippedAt);
        Assert.Equal((sku, "AO-01", "Áo thun, Đen", 4, (decimal?)100_000m), Line(Assert.Single(shipped.Items)));
        Assert.Equal((6, 0, 6), await factory.StockAsync(_tenantId, from, sku));
        Assert.Equal(100_000m, (await factory.BalanceAsync(_tenantId, from, sku))!.AvgCost);
        var transferOut = Assert.Single(await factory.LedgerAsync(_tenantId), line => line.Reason == LedgerReason.TransferOut);
        Assert.Equal(
            (from, LedgerType.OUT, 4, 6, 100_000m, LedgerReference.StockTransfer, draft.Id),
            (transferOut.BranchId, transferOut.Type, transferOut.Quantity, transferOut.BalanceAfter, transferOut.UnitCost,
                transferOut.ReferenceType, transferOut.ReferenceId));
        Assert.NotNull(transferOut.CreatedBy);
        // T12, AC-3: nơi nhận chưa xác nhận thì tồn khả dụng của nó chưa tăng, và hàng đang đi đường không bán được ở đó.
        Assert.Equal((6, 0, 6), await factory.StockAsync(_tenantId, to, sku));
        var oversell = await Staff.PostAsJsonAsync("/orders", new { branchId = to, items = new[] { new { skuId = sku, quantity = 7 } } });
        await oversell.AssertProblemAsync(HttpStatusCode.Conflict, "insufficient_stock");
        // AC-5: tồn nơi gửi cộng hàng đang vận chuyển cộng tồn nơi nhận không đổi.
        Assert.Equal(16, await TotalAsync(sku, from, to));

        var received = await PostAsync<TransferDto>(Owner, $"/transfers/{draft.Id}/receive");

        // AC-4: on_hand nơi nhận tăng, giá vốn nơi nhận tính lại bằng đơn giá mang theo: (6 × 130.000 + 4 × 100.000) / 10.
        Assert.Equal("Received", received.Status);
        Assert.NotNull(received.ReceivedAt);
        Assert.Equal((10, 0, 10), await factory.StockAsync(_tenantId, to, sku));
        Assert.Equal(118_000m, (await factory.BalanceAsync(_tenantId, to, sku))!.AvgCost);
        var transferIn = Assert.Single(await factory.LedgerAsync(_tenantId), line => line.Reason == LedgerReason.TransferIn);
        // Hai dòng sổ cùng chứng từ nên đối soát được hai đầu.
        Assert.Equal(
            (to, LedgerType.IN, 4, 10, 100_000m, LedgerReference.StockTransfer, draft.Id),
            (transferIn.BranchId, transferIn.Type, transferIn.Quantity, transferIn.BalanceAfter, transferIn.UnitCost,
                transferIn.ReferenceType, transferIn.ReferenceId));
        Assert.Equal(16, await TotalAsync(sku, from, to));
        Assert.Equal((6, 0, 6), await factory.StockAsync(_tenantId, from, sku));
        // StockChanged của cả hai đầu nằm trong outbox.
        var changes = await factory.OutboxAsync<StockChanged>(_tenantId);
        Assert.Contains(changes, change => (change.BranchId, change.OnHand, change.AvgCost) == (from, 6, 100_000m));
        Assert.Contains(changes, change => (change.BranchId, change.OnHand, change.AvgCost) == (to, 10, 118_000m));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("UseCase", "UC-INV-03 AC-2")]
    public async Task ShipTransfer_SenderShortOfAvailableStock_Returns409AndChangesNothing()
    {
        var (from, to) = (await factory.SeedBranchAsync(_tenantId), await factory.SeedBranchAsync(_tenantId));
        var shirt = await factory.SeedSkuAsync(_tenantId, "AO-01");
        var trousers = await factory.SeedSkuAsync(_tenantId, "QUAN-01");
        await factory.PostLedgerAsync(_tenantId, from, In(shirt, 5), In(trousers, 5));
        // 3 trong 5 áo đang được giữ cho đơn: chỉ còn 2 chuyển đi được.
        await Staff.CreateOrderAsync(from, new { skuId = shirt, quantity = 3 });
        var transfer = await CreateAsync(Staff, from, to, new { skuId = trousers, quantity = 2 }, new { skuId = shirt, quantity = 3 });
        var ledgerBefore = (await factory.LedgerAsync(_tenantId)).Count;
        var outboxBefore = await factory.OutboxRowCountAsync(_tenantId);

        var response = await Staff.PostAsync($"/transfers/{transfer.Id}/ship", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("insufficient_stock", problem.GetProperty("code").GetString());
        var detail = Assert.Single(problem.GetProperty("details").EnumerateArray());
        Assert.Equal(
            (shirt, 3, 2),
            (detail.GetProperty("skuId").GetGuid(), detail.GetProperty("requested").GetInt32(), detail.GetProperty("available").GetInt32()));
        // Không dòng nào được xuất, kể cả dòng đủ hàng; phiếu vẫn Draft.
        Assert.Equal((5, 3, 2), await factory.StockAsync(_tenantId, from, shirt));
        Assert.Equal((5, 0, 5), await factory.StockAsync(_tenantId, from, trousers));
        Assert.Equal(ledgerBefore, (await factory.LedgerAsync(_tenantId)).Count);
        Assert.Equal(outboxBefore, await factory.OutboxRowCountAsync(_tenantId));
        Assert.Equal(StockTransferStatus.Draft, (await StoredAsync(transfer.Id)).Status);
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("UseCase", "UC-INV-03 AC-4")]
    [Trait("UseCase", "UC-INV-03 AC-6")]
    public async Task ReceiveTransfer_Twice_PostsOnceAndANewSkuAtTheReceiverTakesTheCarriedCost()
    {
        var (from, to) = (await factory.SeedBranchAsync(_tenantId), await factory.SeedBranchAsync(_tenantId));
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        await factory.PostLedgerAsync(_tenantId, from, In(sku, 10, 110_000));
        var transfer = await CreateAsync(Staff, from, to, new { skuId = sku, quantity = 4 });

        // Nhận phiếu còn Draft: 409. Xuất lại phiếu đã xuất: 409.
        var receiveDraft = await Staff.PostAsync($"/transfers/{transfer.Id}/receive", null);
        var shipped = await (await Staff.PostAsync($"/transfers/{transfer.Id}/ship", null)).Content.ReadFromJsonAsync<JsonElement>();
        var shipAgain = await Staff.PostAsync($"/transfers/{transfer.Id}/ship", null);
        var first = await PostAsync<TransferDto>(Staff, $"/transfers/{transfer.Id}/receive");
        var ledgerAfterFirst = (await factory.LedgerAsync(_tenantId)).Count;
        var outboxAfterFirst = await factory.OutboxRowCountAsync(_tenantId);
        var second = await Staff.PostAsync($"/transfers/{transfer.Id}/receive", null);
        var shipReceived = await Staff.PostAsync($"/transfers/{transfer.Id}/ship", null);

        await receiveDraft.AssertProblemAsync(HttpStatusCode.Conflict, "invalid_state_transition");
        await shipAgain.AssertProblemAsync(HttpStatusCode.Conflict, "invalid_state_transition");
        await shipReceived.AssertProblemAsync(HttpStatusCode.Conflict, "invalid_state_transition");
        // Giá vốn chỉ trả cho Owner: Staff không thấy đơn giá mang theo trên phiếu.
        Assert.False(shipped.GetProperty("items")[0].TryGetProperty("unitCost", out _));
        // AC-6: nhận lại trả 200 với cùng phiếu, không tác động lần hai.
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var again = (await second.Content.ReadFromJsonAsync<TransferDto>())!;
        Assert.Equal((first.Id, "Received", first.ReceivedAt), (again.Id, again.Status, again.ReceivedAt));
        Assert.Equal(ledgerAfterFirst, (await factory.LedgerAsync(_tenantId)).Count);
        Assert.Equal(outboxAfterFirst, await factory.OutboxRowCountAsync(_tenantId));
        // Nơi nhận chưa từng có SKU này: giá vốn bằng đúng đơn giá mang theo.
        var balance = (await factory.BalanceAsync(_tenantId, to, sku))!;
        Assert.Equal((4, 110_000m), (balance.OnHand, balance.AvgCost));
        Assert.Equal((6, 0, 6), await factory.StockAsync(_tenantId, from, sku));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("UseCase", "UC-INV-03 AC-1")]
    public async Task ShipTransfer_LedgerWriteFails_LeavesTheTransferDraftAndStockUnchanged()
    {
        var (from, to) = (await factory.SeedBranchAsync(_tenantId), await factory.SeedBranchAsync(_tenantId));
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        await factory.PostLedgerAsync(_tenantId, from, In(sku, 10));
        var transfer = await CreateAsync(Staff, from, to, new { skuId = sku, quantity = 4 });
        var outboxBefore = await factory.OutboxRowCountAsync(_tenantId);

        var response = await factory.CreateClientWithFailingLedger(Staff).PostAsync($"/transfers/{transfer.Id}/ship", null);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var stored = await StoredAsync(transfer.Id);
        Assert.Equal((StockTransferStatus.Draft, (DateTimeOffset?)null, (decimal?)null), (stored.Status, stored.ShippedAt, stored.Items.Single().UnitCost));
        Assert.Equal((10, 0, 10), await factory.StockAsync(_tenantId, from, sku));
        Assert.Single(await factory.LedgerAsync(_tenantId));
        Assert.Equal(outboxBefore, await factory.OutboxRowCountAsync(_tenantId));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("UseCase", "UC-INV-03 AC-7")]
    public async Task CreateTransfer_InvalidInputOrUnknownReference_IsRefusedAndSavesNothing()
    {
        var (from, to) = (await factory.SeedBranchAsync(_tenantId), await factory.SeedBranchAsync(_tenantId));
        var closed = await factory.SeedBranchAsync(_tenantId, isActive: false);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        var item = new { skuId = sku, quantity = 1 };

        HttpResponseMessage[] invalid =
        [
            // AC-7: nơi gửi trùng nơi nhận.
            await PostAsync(from, from, item),
            await PostAsync(from, to),
            await Staff.PostAsJsonAsync("/transfers", new { fromBranchId = from, toBranchId = to }),
            await PostAsync(from, to, new { skuId = sku, quantity = 0 }),
            await PostAsync(from, to, new { skuId = Guid.Empty, quantity = 1 }),
            await PostAsync(Guid.Empty, to, item),
            await PostAsync(from, Guid.Empty, item),
        ];
        var unknownBranch = await PostAsync(from, Guid.NewGuid(), item);
        var unknownSku = await PostAsync(from, to, item, new { skuId = Guid.NewGuid(), quantity = 1 });
        var closedBranch = await PostAsync(closed, to, item);

        foreach (var response in invalid)
            await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await unknownBranch.AssertProblemAsync(HttpStatusCode.Conflict, "reference_not_ready");
        await unknownSku.AssertProblemAsync(HttpStatusCode.Conflict, "reference_not_ready");
        await closedBranch.AssertProblemAsync(HttpStatusCode.Conflict, "inactive_reference");
        Assert.Equal(0, await factory.QueryAsync(_tenantId, db => db.StockTransfers.CountAsync()));
    }

    // T14 cho chuyển kho: phiếu, chi nhánh và SKU của tenant khác không dùng được; Cashier không có quyền.
    [Fact]
    [Trait("Scenario", "T14")]
    [Trait("UseCase", "UC-AUTH-03 AC-4")]
    public async Task Transfer_OfAnotherTenantOrByCashier_IsRefusedAndTheTransferIsUnchanged()
    {
        var (from, to) = (await factory.SeedBranchAsync(_tenantId), await factory.SeedBranchAsync(_tenantId));
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        await factory.PostLedgerAsync(_tenantId, from, In(sku, 10));
        var transfer = await CreateAsync(Staff, from, to, new { skuId = sku, quantity = 4 });
        var otherTenantId = Guid.NewGuid();
        var outsider = factory.CreateClient(Roles.Owner, otherTenantId);
        var cashier = factory.CreateClient(Roles.Cashier, _tenantId, from);
        var body = new { fromBranchId = from, toBranchId = to, items = new[] { new { skuId = sku, quantity = 1 } } };

        await (await outsider.PostAsync($"/transfers/{transfer.Id}/ship", null)).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await outsider.PostAsync($"/transfers/{transfer.Id}/receive", null)).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        // Chi nhánh của tenant khác gửi trong body: 409 reference_not_ready (docs/architecture/multi-tenancy.md quy tắc 3).
        await (await outsider.PostAsJsonAsync("/transfers", body)).AssertProblemAsync(HttpStatusCode.Conflict, "reference_not_ready");
        await (await cashier.PostAsJsonAsync("/transfers", body)).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        await (await cashier.PostAsync($"/transfers/{transfer.Id}/ship", null)).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        await (await cashier.PostAsync($"/transfers/{transfer.Id}/receive", null)).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");

        Assert.Equal((_tenantId, StockTransferStatus.Draft), ((await StoredAsync(transfer.Id)).TenantId, (await StoredAsync(transfer.Id)).Status));
        Assert.Equal(0, await factory.QueryAsync(otherTenantId, db => db.StockTransfers.CountAsync()));
        Assert.Equal((10, 0, 10), await factory.StockAsync(_tenantId, from, sku));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("UseCase", "UC-INV-03 AC-3")]
    public async Task ListTransfers_ByStatus_ShowsDraftsFirstThenTheLatestShippedAndHidesCostFromStaff()
    {
        var (from, to) = (await factory.SeedBranchAsync(_tenantId), await factory.SeedBranchAsync(_tenantId));
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01", "Áo thun, Đen");
        await factory.PostLedgerAsync(_tenantId, from, In(sku, 10, 100_000));
        var staff = Staff;
        var received = await CreateAsync(staff, from, to, new { skuId = sku, quantity = 1 });
        await staff.PostAsync($"/transfers/{received.Id}/ship", null);
        await staff.PostAsync($"/transfers/{received.Id}/receive", null);
        var inTransit = await CreateAsync(staff, from, to, new { skuId = sku, quantity = 2 });
        await staff.PostAsync($"/transfers/{inTransit.Id}/ship", null);
        var draft = await CreateAsync(staff, from, to, new { skuId = sku, quantity = 3 });
        // Phiếu của tenant khác không lọt vào danh sách.
        var otherTenantId = Guid.NewGuid();
        var (fromOfOther, toOfOther) = (await factory.SeedBranchAsync(otherTenantId), await factory.SeedBranchAsync(otherTenantId));
        await CreateAsync(factory.CreateClient(Roles.Staff, otherTenantId), fromOfOther, toOfOther, new { skuId = await factory.SeedSkuAsync(otherTenantId, "B-01"), quantity = 1 });

        var all = (await Owner.GetFromJsonAsync<PagedResult<TransferDto>>("/transfers"))!;
        var onlyInTransit = (await staff.GetFromJsonAsync<PagedResult<TransferDto>>("/transfers?status=InTransit"))!;
        var secondPage = (await staff.GetFromJsonAsync<PagedResult<TransferDto>>("/transfers?page=2&pageSize=2"))!;
        var asStaff = await staff.GetFromJsonAsync<JsonElement>("/transfers?status=InTransit");
        HttpResponseMessage[] invalid =
        [
            await staff.GetAsync("/transfers?page=0"),
            await staff.GetAsync("/transfers?pageSize=101"),
            await staff.GetAsync("/transfers?status=Cancelled"),
        ];

        // Phiếu chưa xuất đứng trước, rồi tới phiếu xuất gần nhất.
        Assert.Equal((3, 1, 20), (all.Total, all.Page, all.PageSize));
        Assert.Equal([draft.Id, inTransit.Id, received.Id], all.Items.Select(transfer => transfer.Id));
        Assert.Equal(["Draft", "InTransit", "Received"], all.Items.Select(transfer => transfer.Status));
        // Mỗi phiếu kèm các dòng của nó; Owner thấy giá vốn mang theo của phiếu đã xuất, Staff thì không.
        Assert.Equal((sku, "AO-01", "Áo thun, Đen", 3, (decimal?)null), Line(Assert.Single(all.Items[0].Items)));
        Assert.Equal((sku, "AO-01", "Áo thun, Đen", 2, (decimal?)100_000m), Line(Assert.Single(all.Items[1].Items)));
        Assert.Equal(inTransit.Id, Assert.Single(onlyInTransit.Items).Id);
        Assert.False(asStaff.GetProperty("items")[0].GetProperty("items")[0].TryGetProperty("unitCost", out _));
        Assert.Equal((3, received.Id), (secondPage.Total, Assert.Single(secondPage.Items).Id));
        foreach (var response in invalid)
            await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await factory.CreateClient(Roles.Cashier, _tenantId, from).GetAsync("/transfers")).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    // ADR-0009: chỉ xóa được phiếu khi còn Draft; đã InTransit thì không hủy.
    [Fact]
    [Trait("UseCase", "UC-INV-03 AC-8")]
    [Trait("Scenario", "T14")]
    public async Task DeleteTransfer_OnlyWhileDraft_RemovesItWithoutTouchingStock()
    {
        var (from, to) = (await factory.SeedBranchAsync(_tenantId), await factory.SeedBranchAsync(_tenantId));
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        await factory.PostLedgerAsync(_tenantId, from, In(sku, 10));
        var staff = Staff;
        var draft = await CreateAsync(staff, from, to, new { skuId = sku, quantity = 4 });
        var shipped = await CreateAsync(staff, from, to, new { skuId = sku, quantity = 2 });
        await staff.PostAsync($"/transfers/{shipped.Id}/ship", null);
        var ledgerBefore = (await factory.LedgerAsync(_tenantId)).Count;
        var outboxBefore = await factory.OutboxRowCountAsync(_tenantId);

        var asOtherTenant = await factory.CreateClient(Roles.Owner, Guid.NewGuid()).DeleteAsync($"/transfers/{draft.Id}");
        var asCashier = await factory.CreateClient(Roles.Cashier, _tenantId, from).DeleteAsync($"/transfers/{draft.Id}");
        var deleted = await staff.DeleteAsync($"/transfers/{draft.Id}");
        var deletedAgain = await staff.DeleteAsync($"/transfers/{draft.Id}");
        var inTransit = await staff.DeleteAsync($"/transfers/{shipped.Id}");
        await staff.PostAsync($"/transfers/{shipped.Id}/receive", null);
        var received = await staff.DeleteAsync($"/transfers/{shipped.Id}");

        await asOtherTenant.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await asCashier.AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        await deletedAgain.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await inTransit.AssertProblemAsync(HttpStatusCode.Conflict, "invalid_state_transition");
        await received.AssertProblemAsync(HttpStatusCode.Conflict, "invalid_state_transition");
        // Phiếu Draft và các dòng của nó biến mất; phiếu đã xuất còn nguyên. Xóa không đụng tới tồn, sổ hay outbox.
        Assert.Equal([shipped.Id], await factory.QueryAsync(_tenantId, db => db.StockTransfers.Select(transfer => transfer.Id).ToListAsync()));
        Assert.Equal([shipped.Id], await factory.QueryAsync(_tenantId, db => db.StockTransferItems.Select(item => item.TransferId).ToListAsync()));
        Assert.Equal((8, 0, 8), await factory.StockAsync(_tenantId, from, sku));
        Assert.Equal(ledgerBefore + 1, (await factory.LedgerAsync(_tenantId)).Count);
        Assert.Equal(outboxBefore + 1, await factory.OutboxRowCountAsync(_tenantId));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    private static (Guid SkuId, string SkuCode, string SkuName, int Quantity, decimal? UnitCost) Line(TransferItemDto item) =>
        (item.SkuId, item.SkuCode, item.SkuName, item.Quantity, item.UnitCost);

    private Task<HttpResponseMessage> PostAsync(Guid fromBranchId, Guid toBranchId, params object[] items) =>
        Staff.PostAsJsonAsync("/transfers", new { fromBranchId, toBranchId, items });

    private static async Task<TransferDto> CreateAsync(HttpClient client, Guid fromBranchId, Guid toBranchId, params object[] items)
    {
        var response = await client.PostAsJsonAsync("/transfers", new { fromBranchId, toBranchId, items });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TransferDto>())!;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string path)
    {
        var response = await client.PostAsync(path, null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private Task<StockTransfer> StoredAsync(Guid id) =>
        factory.QueryAsync(_tenantId, db => db.StockTransfers.AsNoTracking().Include(transfer => transfer.Items).SingleAsync(transfer => transfer.Id == id));

    // Tồn nơi gửi cộng số lượng trên các phiếu InTransit cộng tồn nơi nhận (bất biến 9).
    private async Task<int> TotalAsync(Guid skuId, Guid from, Guid to)
    {
        var inTransit = await factory.QueryAsync(_tenantId, db => db.StockTransfers.AsNoTracking()
            .Where(transfer => transfer.Status == StockTransferStatus.InTransit)
            .SelectMany(transfer => transfer.Items).Where(item => item.SkuId == skuId).SumAsync(item => item.Quantity));
        return (await factory.BalanceAsync(_tenantId, from, skuId))!.OnHand + inTransit + (await factory.BalanceAsync(_tenantId, to, skuId))!.OnHand;
    }
}
