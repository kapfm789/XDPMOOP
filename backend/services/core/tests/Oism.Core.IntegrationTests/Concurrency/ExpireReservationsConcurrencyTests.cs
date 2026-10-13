using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Auth;
using Oism.Contracts;
using Oism.Core.Domain.Inventory;
using Oism.Core.Domain.Orders;
using Oism.Core.Infrastructure.Jobs;
using static Oism.Core.IntegrationTests.TestKit;

namespace Oism.Core.IntegrationTests.Concurrency;

// W3-04: nhân viên duyệt đúng lúc job hết hạn chạy trên cùng một đơn. Ai khóa được dòng đơn trước thì thắng.
public sealed class ExpireReservationsConcurrencyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Race không lộ ra ở mọi lần chạy: lặp 20 lần (docs/testing/strategy.md mục "Các tầng test").
    public static IEnumerable<object[]> Rounds => Enumerable.Range(1, 20).Select(round => new object[] { round });

    [Theory]
    [MemberData(nameof(Rounds))]
    [Trait("Scenario", "T07")]
    [Trait("UseCase", "UC-ORD-03 AC-5")]
    public async Task ConfirmAndExpiryJob_SameExpiredOrderAtTheSameTime_ExactlyOneWins(int round)
    {
        var tenantId = Guid.NewGuid();
        var branch = await factory.SeedBranchAsync(tenantId);
        var sku = await factory.SeedSkuAsync(tenantId, $"AO-{round}");
        await factory.PostLedgerAsync(tenantId, branch, In(sku, 5));
        var staff = factory.CreateClient(Roles.Staff, tenantId);
        var order = await staff.CreateOrderAsync(branch, new { skuId = sku, quantity = 2 });
        await factory.ExpireHoldsAsync(tenantId, order.Id);
        var job = factory.Services.GetRequiredService<ExpireReservationsJob>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var confirm = Task.Run(async () =>
        {
            await gate.Task;
            return await staff.PostAsync($"/orders/{order.Id}/confirm", null);
        });
        var expire = Task.Run(async () =>
        {
            await gate.Task;
            await job.RunAsync(default);
        });
        gate.SetResult();
        await expire;
        var response = await confirm;

        // Đơn kết thúc ở đúng một trạng thái: không có chuyện vừa có dòng sổ OUT vừa có phần giữ Released.
        var stored = Assert.Single(await factory.OrdersAsync(tenantId));
        var hold = Assert.Single(await factory.ReservationsAsync(tenantId));
        var sales = (await factory.LedgerAsync(tenantId)).Count(line => line.Reason == LedgerReason.Sale);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            Assert.Equal((OrderStatus.Confirmed, ReservationStatus.Consumed, 1), (stored.Status, hold.Status, sales));
            Assert.Equal((3, 0, 3), await factory.StockAsync(tenantId, branch, sku));
            Assert.Empty(await factory.OutboxAsync<OrderCancelled>(tenantId));
            Assert.Single(await factory.OutboxAsync<OrderConfirmed>(tenantId));
        }
        else
        {
            await response.AssertProblemAsync(HttpStatusCode.Conflict, "invalid_state_transition");
            Assert.Equal((OrderStatus.Cancelled, ReservationStatus.Released, 0), (stored.Status, hold.Status, sales));
            Assert.Equal(OrderCancelReason.Expired, stored.CancelReason);
            Assert.Equal((5, 0, 5), await factory.StockAsync(tenantId, branch, sku));
            Assert.Empty(await factory.OutboxAsync<OrderConfirmed>(tenantId));
            Assert.Single(await factory.OutboxAsync<OrderCancelled>(tenantId));
        }

        await factory.AssertInventoryInvariantsAsync(tenantId);
    }
}
