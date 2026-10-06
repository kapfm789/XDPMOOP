using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Auth;
using Oism.Contracts;
using Oism.Core.Application.Orders;
using Oism.Core.Application.Orders.CancelOrder;
using Oism.Core.Domain.Orders;
using static Oism.Core.IntegrationTests.TestKit;

namespace Oism.Core.IntegrationTests.Orders;

// W3-01: hủy đơn chưa xuất kho trả hàng đã giữ về tồn khả dụng (UC-ORD-04; FR-ORD-03, FR-RSE-03).
public sealed class CancelOrderTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private HttpClient Staff => factory.CreateClient(Roles.Staff, _tenantId);

    [Fact]
    [Trait("UseCase", "UC-ORD-04 AC-1")]
    [Trait("UseCase", "UC-ORD-04 AC-4")]
    public async Task CancelOrder_ReservedOrder_ReleasesEveryHoldWithoutTouchingOnHandOrLedger()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var shirt = await factory.SeedSkuAsync(_tenantId, "AO-01");
        var trousers = await factory.SeedSkuAsync(_tenantId, "QUAN-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(shirt, 5), In(trousers, 5));
        var order = await Staff.CreateOrderAsync(branch, new { skuId = shirt, quantity = 2 }, new { skuId = trousers, quantity = 1 });
        // Đơn khác vẫn giữ hàng của nó sau khi đơn này bị hủy.
        var other = await Staff.CreateOrderAsync(branch, new { skuId = shirt, quantity = 1 });
        var ledgerBefore = (await factory.LedgerAsync(_tenantId)).Count;

        var response = await Staff.PostAsync($"/orders/{order.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cancelled = (await response.Content.ReadFromJsonAsync<OrderDto>())!;
        Assert.Equal((order.Id, "Cancelled", "Manual"), (cancelled.Id, cancelled.Status, cancelled.CancelReason));
        Assert.NotNull(cancelled.CancelledAt);
        // reserved giảm đúng bằng phần giữ của đơn; on_hand không đổi và không có dòng sổ mới.
        Assert.Equal((5, 1, 4), await factory.StockAsync(_tenantId, branch, shirt));
        Assert.Equal((5, 0, 5), await factory.StockAsync(_tenantId, branch, trousers));
        Assert.Equal(ledgerBefore, (await factory.LedgerAsync(_tenantId)).Count);
        var holds = await factory.ReservationsAsync(_tenantId);
        Assert.All(holds.Where(hold => hold.OrderId == order.Id), hold =>
        {
            Assert.Equal(ReservationStatus.Released, hold.Status);
            Assert.NotNull(hold.ClosedAt);
        });
        Assert.Equal(ReservationStatus.Active, Assert.Single(holds, hold => hold.OrderId == other.Id).Status);

        var message = Assert.Single(await factory.OutboxAsync<OrderCancelled>(_tenantId));
        Assert.Equal(
            (order.Id, "Admin", (string?)null, branch, "Manual", cancelled.CancelledAt),
            (message.OrderId, message.Channel, message.ExternalOrderId, message.BranchId, message.Reason, (DateTimeOffset?)message.CancelledAt));
        Assert.Contains(
            await factory.OutboxAsync<StockChanged>(_tenantId),
            change => (change.SkuId, change.OnHand, change.Reserved, change.Available) == (trousers, 5, 0, 5));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    // Phần của T06 thuộc W3-01: hủy hai lần, rồi handler được gọi lại với lý do Expired như job hết hạn sẽ gọi (W3-04).
    [Fact]
    [Trait("Scenario", "T06")]
    [Trait("UseCase", "UC-ORD-04 AC-2")]
    [Trait("UseCase", "UC-ORD-05 AC-2")]
    public async Task CancelOrder_Twice_ReleasesTheHoldOnceAndReturnsTheSameOrder()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(sku, 5));
        var kept = await Staff.CreateOrderAsync(branch, new { skuId = sku, quantity = 1 });
        var order = await Staff.CreateOrderAsync(branch, new { skuId = sku, quantity = 2 });

        var first = (await (await Staff.PostAsync($"/orders/{order.Id}/cancel", null)).Content.ReadFromJsonAsync<OrderDto>())!;
        var outboxAfterFirst = await factory.OutboxRowCountAsync(_tenantId);
        var versionAfterFirst = (await factory.BalanceAsync(_tenantId, branch, sku))!.Version;
        var second = await Staff.PostAsync($"/orders/{order.Id}/cancel", null);
        var expired = await factory.InTenantAsync(_tenantId, services =>
            services.GetRequiredService<CancelOrderHandler>().Handle(new CancelOrderCommand(order.Id, OrderCancelReason.Expired), default));

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var again = (await second.Content.ReadFromJsonAsync<OrderDto>())!;
        Assert.Equal((first.Id, "Cancelled", "Manual", first.CancelledAt), (again.Id, again.Status, again.CancelReason, again.CancelledAt));
        Assert.Equal(("Cancelled", "Manual", first.CancelledAt), (expired.Status, expired.CancelReason, expired.CancelledAt));
        // reserved chỉ giảm một lần: còn đúng phần giữ của đơn kia.
        Assert.Equal((5, 1, 4), await factory.StockAsync(_tenantId, branch, sku));
        Assert.Equal(versionAfterFirst, (await factory.BalanceAsync(_tenantId, branch, sku))!.Version);
        Assert.Equal(outboxAfterFirst, await factory.OutboxRowCountAsync(_tenantId));
        Assert.Single(await factory.OutboxAsync<OrderCancelled>(_tenantId));
        Assert.Equal(ReservationStatus.Active, Assert.Single(await factory.ReservationsAsync(_tenantId), hold => hold.OrderId == kept.Id).Status);
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    // Đường vào của job hết hạn (UC-ORD-05 AC-1): cùng handler, lý do Expired. Bản thân job thuộc W3-04.
    [Fact]
    [Trait("UseCase", "UC-ORD-05 AC-1")]
    public async Task CancelOrder_WithReasonExpired_CancelsAsExpiredAndReleasesTheHold()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(sku, 5));
        var order = await Staff.CreateOrderAsync(branch, new { skuId = sku, quantity = 2 });

        var cancelled = await factory.InTenantAsync(_tenantId, services =>
            services.GetRequiredService<CancelOrderHandler>().Handle(new CancelOrderCommand(order.Id, OrderCancelReason.Expired), default));

        Assert.Equal(("Cancelled", "Expired"), (cancelled.Status, cancelled.CancelReason));
        Assert.Equal((5, 0, 5), await factory.StockAsync(_tenantId, branch, sku));
        Assert.Equal("Expired", Assert.Single(await factory.OutboxAsync<OrderCancelled>(_tenantId)).Reason);
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("Scenario", "T08")]
    [Trait("UseCase", "UC-ORD-04 AC-3")]
    public async Task CancelOrder_AfterConfirmedOrCompleted_Returns409AndStockIsUnchanged()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(sku, 5));
        // Một đơn khác còn giữ hàng: reserved khác 0 để thấy rõ nó không bị giảm nhầm.
        await Staff.CreateOrderAsync(branch, new { skuId = sku, quantity = 1 });
        var order = await Staff.CreateOrderAsync(branch, new { skuId = sku, quantity = 2 });
        await Staff.PostAsync($"/orders/{order.Id}/confirm", null);
        var ledgerBefore = (await factory.LedgerAsync(_tenantId)).Count;
        var outboxBefore = await factory.OutboxRowCountAsync(_tenantId);

        var afterConfirmed = await Staff.PostAsync($"/orders/{order.Id}/cancel", null);
        await Staff.PostAsync($"/orders/{order.Id}/complete", null);
        var afterCompleted = await Staff.PostAsync($"/orders/{order.Id}/cancel", null);
        var unknown = await Staff.PostAsync($"/orders/{Guid.NewGuid()}/cancel", null);

        await afterConfirmed.AssertProblemAsync(HttpStatusCode.Conflict, "invalid_state_transition");
        await afterCompleted.AssertProblemAsync(HttpStatusCode.Conflict, "invalid_state_transition");
        await unknown.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        Assert.Equal((3, 1, 2), await factory.StockAsync(_tenantId, branch, sku));
        Assert.Equal(ledgerBefore, (await factory.LedgerAsync(_tenantId)).Count);
        Assert.Equal(outboxBefore, await factory.OutboxRowCountAsync(_tenantId));
        Assert.Empty(await factory.OutboxAsync<OrderCancelled>(_tenantId));
        Assert.Equal(
            (OrderStatus.Completed, (OrderCancelReason?)null),
            (await factory.OrdersAsync(_tenantId)).Where(stored => stored.Id == order.Id).Select(stored => (stored.Status, stored.CancelReason)).Single());
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }
}
