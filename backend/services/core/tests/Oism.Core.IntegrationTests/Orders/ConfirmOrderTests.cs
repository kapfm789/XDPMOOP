using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Oism.BuildingBlocks.Auth;
using Oism.Contracts;
using Oism.Core.Application.Orders;
using Oism.Core.Domain.Inventory;
using Oism.Core.Domain.Orders;
using static Oism.Core.IntegrationTests.TestKit;

namespace Oism.Core.IntegrationTests.Orders;

// W3-01: duyệt đơn là lúc hàng rời kho, và hoàn tất đơn (UC-ORD-03, UC-ORD-06; FR-RSE-03, FR-COST-02, FR-ORD-03).
public sealed class ConfirmOrderTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private HttpClient Staff => factory.CreateClient(Roles.Staff, _tenantId);

    private HttpClient Owner => factory.CreateClient(Roles.Owner, _tenantId);

    [Fact]
    [Trait("UseCase", "UC-ORD-03 AC-1")]
    [Trait("UseCase", "UC-ORD-03 AC-2")]
    public async Task ConfirmOrder_ReservedOrder_ShipsTheStockFixesTheCostAndEmitsOrderConfirmed()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var shirt = await factory.SeedSkuAsync(_tenantId, "AO-01", retailPrice: 150_000);
        var trousers = await factory.SeedSkuAsync(_tenantId, "QUAN-01", retailPrice: 80_000);
        await factory.PostLedgerAsync(_tenantId, branch, In(shirt, 5, 110_000), In(trousers, 5, 40_000));
        var order = await Staff.CreateOrderAsync(
            branch, new { skuId = shirt, quantity = 2 }, new { skuId = trousers, quantity = 1, discount = 5_000 });
        Assert.Equal((5, 2, 3), await factory.StockAsync(_tenantId, branch, shirt));

        var response = await Owner.PostAsync($"/orders/{order.Id}/confirm", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var confirmed = (await response.Content.ReadFromJsonAsync<OrderDto>())!;
        Assert.Equal((order.Id, "Confirmed"), (confirmed.Id, confirmed.Status));
        Assert.NotNull(confirmed.ConfirmedAt);
        // AC-1: on_hand và reserved cùng giảm đúng số lượng; tồn khả dụng không đổi.
        Assert.Equal((3, 0, 3), await factory.StockAsync(_tenantId, branch, shirt));
        Assert.Equal((4, 0, 4), await factory.StockAsync(_tenantId, branch, trousers));
        // AC-2: mỗi dòng đơn mang giá vốn bình quân lúc duyệt; Owner thấy nó trong phản hồi.
        Assert.Equal(
            new[] { (shirt, (decimal?)110_000m), (trousers, (decimal?)40_000m) }.Order(),
            confirmed.Items.Select(item => (item.SkuId, item.CostPrice)).Order());
        var stored = Assert.Single(await factory.OrdersAsync(_tenantId));
        Assert.Equal((OrderStatus.Confirmed, confirmed.ConfirmedAt), (stored.Status, stored.ConfirmedAt));
        Assert.Equal(
            new[] { (shirt, (decimal?)110_000m), (trousers, (decimal?)40_000m) }.Order(),
            stored.Items.Select(item => (item.SkuId, item.CostPrice)).Order());
        // AC-2: sổ có một dòng OUT lý do Sale cho mỗi dòng đơn, đơn giá là giá vốn, chứng từ là đơn.
        var sales = (await factory.LedgerAsync(_tenantId)).Where(line => line.Reason == LedgerReason.Sale).ToList();
        Assert.Equal(
            new[] { (shirt, LedgerType.OUT, 2, 3, 110_000m), (trousers, LedgerType.OUT, 1, 4, 40_000m) }.Order(),
            sales.Select(line => (line.SkuId, line.Type, line.Quantity, line.BalanceAfter, line.UnitCost)).Order());
        Assert.All(sales, line => Assert.Equal((LedgerReference.Order, order.Id), (line.ReferenceType, line.ReferenceId)));
        Assert.All(sales, line => Assert.NotNull(line.CreatedBy));
        // Phần giữ sang Consumed.
        Assert.All(await factory.ReservationsAsync(_tenantId), hold =>
        {
            Assert.Equal(ReservationStatus.Consumed, hold.Status);
            Assert.NotNull(hold.ClosedAt);
        });

        // OrderConfirmed kèm giá bán và giá vốn từng dòng, và StockChanged mang số dư sau khi xuất, nằm trong outbox.
        var message = Assert.Single(await factory.OutboxAsync<OrderConfirmed>(_tenantId));
        Assert.Equal(
            (order.Id, "Admin", branch, confirmed.ConfirmedAt),
            (message.OrderId, message.Channel, message.BranchId, (DateTimeOffset?)message.ConfirmedAt));
        Assert.Equal(
            confirmed.Items.Select(item => new OrderConfirmedLine(
                item.Id, item.SkuId, item.Quantity, item.UnitPrice, item.Discount, item.CostPrice!.Value)).OrderBy(line => line.SkuId),
            message.Lines.OrderBy(line => line.SkuId));
        var stockChanges = await factory.OutboxAsync<StockChanged>(_tenantId);
        Assert.Contains(stockChanges, change => (change.BranchId, change.SkuId, change.OnHand, change.Reserved, change.Available) == (branch, shirt, 3, 0, 3));
        Assert.Contains(stockChanges, change => (change.BranchId, change.SkuId, change.OnHand, change.Reserved, change.Available) == (branch, trousers, 4, 0, 4));
        Assert.Equal(
            (await factory.BalanceAsync(_tenantId, branch, shirt))!.Version,
            stockChanges.Where(change => change.SkuId == shirt).Max(change => change.Version));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("UseCase", "UC-ORD-03 AC-2")]
    [Trait("UseCase", "UC-ORD-06 AC-1")]
    [Trait("UseCase", "UC-ORD-06 AC-2")]
    public async Task CompleteOrder_ConfirmedOrder_IsCompletedWithoutTouchingStockAndStaffNeverSeesTheCost()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(sku, 5, 110_000));
        var order = await Staff.CreateOrderAsync(branch, new { skuId = sku, quantity = 2 });

        // Chưa duyệt thì chưa hoàn tất được.
        await (await Staff.PostAsync($"/orders/{order.Id}/complete", null)).AssertProblemAsync(HttpStatusCode.Conflict, "invalid_state_transition");
        var confirmed = await (await Staff.PostAsync($"/orders/{order.Id}/confirm", null)).Content.ReadFromJsonAsync<JsonElement>();
        var ledgerBefore = (await factory.LedgerAsync(_tenantId)).Count;
        var outboxBefore = (await factory.OutboxRowCountAsync(_tenantId));

        var response = await Staff.PostAsync($"/orders/{order.Id}/complete", null);
        var again = await Staff.PostAsync($"/orders/{order.Id}/complete", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var completed = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Completed", completed.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, completed.GetProperty("completedAt").ValueKind);
        // Giá vốn chỉ trả cho Owner: với Staff trường này không nằm trong phản hồi, dù đã được chốt trong database.
        Assert.False(confirmed.GetProperty("items")[0].TryGetProperty("costPrice", out _));
        Assert.False(completed.GetProperty("items")[0].TryGetProperty("costPrice", out _));
        Assert.Equal(110_000m, Assert.Single(Assert.Single(await factory.OrdersAsync(_tenantId)).Items).CostPrice);
        // UC-ORD-06 AC-1: tồn và sổ không đổi, không có event mới. AC-2: hoàn tất lại bị từ chối.
        Assert.Equal((3, 0, 3), await factory.StockAsync(_tenantId, branch, sku));
        Assert.Equal(ledgerBefore, (await factory.LedgerAsync(_tenantId)).Count);
        Assert.Equal(outboxBefore, await factory.OutboxRowCountAsync(_tenantId));
        await again.AssertProblemAsync(HttpStatusCode.Conflict, "invalid_state_transition");
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("UseCase", "UC-ORD-03 AC-3")]
    public async Task ConfirmOrder_NotReserved_Returns409AndChangesNothing()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(sku, 5));
        var confirmedOrder = await Staff.CreateOrderAsync(branch, new { skuId = sku, quantity = 2 });
        var cancelledOrder = await Staff.CreateOrderAsync(branch, new { skuId = sku, quantity = 1 });
        await Staff.PostAsync($"/orders/{confirmedOrder.Id}/confirm", null);
        await Staff.PostAsync($"/orders/{cancelledOrder.Id}/cancel", null);
        var ledgerBefore = (await factory.LedgerAsync(_tenantId)).Count;
        var outboxBefore = await factory.OutboxRowCountAsync(_tenantId);

        var confirmAgain = await Staff.PostAsync($"/orders/{confirmedOrder.Id}/confirm", null);
        var confirmCancelled = await Staff.PostAsync($"/orders/{cancelledOrder.Id}/confirm", null);
        var unknown = await Staff.PostAsync($"/orders/{Guid.NewGuid()}/confirm", null);

        await confirmAgain.AssertProblemAsync(HttpStatusCode.Conflict, "invalid_state_transition");
        await confirmCancelled.AssertProblemAsync(HttpStatusCode.Conflict, "invalid_state_transition");
        await unknown.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        // Duyệt hai lần chỉ xuất hàng một lần.
        Assert.Equal((3, 0, 3), await factory.StockAsync(_tenantId, branch, sku));
        Assert.Equal(ledgerBefore, (await factory.LedgerAsync(_tenantId)).Count);
        Assert.Equal(outboxBefore, await factory.OutboxRowCountAsync(_tenantId));
        Assert.Single(await factory.OutboxAsync<OrderConfirmed>(_tenantId));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("Scenario", "T05")]
    [Trait("UseCase", "UC-ORD-03 AC-4")]
    public async Task ConfirmOrder_LedgerWriteFails_LeavesOrderStockHoldsCostLedgerAndOutboxAsBefore()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var shirt = await factory.SeedSkuAsync(_tenantId, "AO-01");
        var trousers = await factory.SeedSkuAsync(_tenantId, "QUAN-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(shirt, 5), In(trousers, 5));
        var order = await Staff.CreateOrderAsync(branch, new { skuId = shirt, quantity = 2 }, new { skuId = trousers, quantity = 1 });
        var ledgerBefore = (await factory.LedgerAsync(_tenantId)).Count;
        var outboxBefore = await factory.OutboxRowCountAsync(_tenantId);
        var versionBefore = (await factory.BalanceAsync(_tenantId, branch, shirt))!.Version;

        var response = await factory.CreateClientWithFailingLedger(Staff).PostAsync($"/orders/{order.Id}/confirm", null);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var stored = Assert.Single(await factory.OrdersAsync(_tenantId));
        Assert.Equal((OrderStatus.Reserved, (DateTimeOffset?)null), (stored.Status, stored.ConfirmedAt));
        Assert.All(stored.Items, item => Assert.Null(item.CostPrice));
        Assert.Equal((5, 2, 3), await factory.StockAsync(_tenantId, branch, shirt));
        Assert.Equal((5, 1, 4), await factory.StockAsync(_tenantId, branch, trousers));
        Assert.Equal(versionBefore, (await factory.BalanceAsync(_tenantId, branch, shirt))!.Version);
        Assert.All(await factory.ReservationsAsync(_tenantId), hold => Assert.Equal(ReservationStatus.Active, hold.Status));
        Assert.Equal(ledgerBefore, (await factory.LedgerAsync(_tenantId)).Count);
        Assert.Equal(outboxBefore, await factory.OutboxRowCountAsync(_tenantId));

        // Hết lỗi thì duyệt lại được như chưa có gì xảy ra.
        Assert.Equal(HttpStatusCode.OK, (await Staff.PostAsync($"/orders/{order.Id}/confirm", null)).StatusCode);
        Assert.Equal((3, 0, 3), await factory.StockAsync(_tenantId, branch, shirt));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    // Nửa của T10 nằm ở `core`: nhập lô mới làm đổi giá vốn bình quân không đổi giá vốn đã chốt của đơn cũ.
    [Fact]
    [Trait("Scenario", "T10")]
    [Trait("UseCase", "UC-ORD-03 AC-6")]
    public async Task ConfirmOrder_ThenReceiveANewLotAtAnotherPrice_CostPriceOfTheOrderStays()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(sku, 10, 100_000), In(sku, 5, 130_000));
        var order = await Staff.CreateOrderAsync(branch, new { skuId = sku, quantity = 2 });
        await Staff.PostAsync($"/orders/{order.Id}/confirm", null);

        // Còn 13 đơn vị giá vốn 110.000; nhập 13 đơn vị giá 130.000 đưa giá vốn lên 120.000.
        await factory.PostLedgerAsync(_tenantId, branch, In(sku, 13, 130_000));

        Assert.Equal(120_000m, (await factory.BalanceAsync(_tenantId, branch, sku))!.AvgCost);
        Assert.Equal(110_000m, Assert.Single(Assert.Single(await factory.OrdersAsync(_tenantId)).Items).CostPrice);
        Assert.Equal(110_000m, Assert.Single(Assert.Single(await factory.OutboxAsync<OrderConfirmed>(_tenantId)).Lines).CostPrice);
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("Scenario", "T14")]
    [Trait("UseCase", "UC-AUTH-03 AC-4")]
    public async Task ConfirmCompleteCancel_OrderOfAnotherTenantOrCashier_IsRefusedAndTheOrderIsUnchanged()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(sku, 5));
        var order = await Staff.CreateOrderAsync(branch, new { skuId = sku, quantity = 2 });
        var outsider = factory.CreateClient(Roles.Owner, Guid.NewGuid());
        var cashier = factory.CreateClient(Roles.Cashier, _tenantId, branch);

        foreach (var action in new[] { "confirm", "complete", "cancel" })
        {
            await (await outsider.PostAsync($"/orders/{order.Id}/{action}", null)).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
            await (await cashier.PostAsync($"/orders/{order.Id}/{action}", null)).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        }

        Assert.Equal(OrderStatus.Reserved, Assert.Single(await factory.OrdersAsync(_tenantId)).Status);
        Assert.Equal((5, 2, 3), await factory.StockAsync(_tenantId, branch, sku));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }
}
