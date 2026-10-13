using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Auth;
using Oism.Contracts;
using Oism.Core.Application;
using Oism.Core.Domain.Orders;
using Oism.Core.Infrastructure.Jobs;
using static Oism.Core.IntegrationTests.TestKit;

namespace Oism.Core.IntegrationTests.Orders;

// W3-04: job hủy đơn Reserved có phần giữ đã quá hạn (UC-ORD-05; FR-SIM-03, FR-RSE-03).
// Lịch chạy mỗi phút của Hangfire tắt trong test (ApiFactory); test tự gọi job, trừ test cuối chạy qua Hangfire thật.
public sealed class ExpireReservationsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private HttpClient Staff => factory.CreateClient(Roles.Staff, _tenantId);

    private ExpireReservationsJob Job => factory.Services.GetRequiredService<ExpireReservationsJob>();

    [Fact]
    [Trait("Scenario", "T06")]
    [Trait("UseCase", "UC-ORD-05 AC-1")]
    [Trait("UseCase", "UC-ORD-05 AC-2")]
    [Trait("UseCase", "UC-ORD-05 AC-3")]
    [Trait("UseCase", "UC-ORD-05 AC-5")]
    public async Task ExpireReservations_ExpiredOrdersOfTwoTenants_CancelsEachInItsOwnTenantOnceAndLeavesFreshAndConfirmedOrdersAlone()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var shirt = await factory.SeedSkuAsync(_tenantId, "AO-01");
        var trousers = await factory.SeedSkuAsync(_tenantId, "QUAN-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(shirt, 10), In(trousers, 10));
        var expired = await Staff.CreateOrderAsync(branch, new { skuId = shirt, quantity = 2 }, new { skuId = trousers, quantity = 1 });
        var fresh = await Staff.CreateOrderAsync(branch, new { skuId = shirt, quantity = 3 });
        // Đơn đã được duyệt trước khi job tới, dù hạn giữ đã qua.
        var confirmed = await Staff.CreateOrderAsync(branch, new { skuId = shirt, quantity = 1 });
        await factory.ExpireHoldsAsync(_tenantId, confirmed.Id);
        await Staff.PostAsync($"/orders/{confirmed.Id}/confirm", null);
        await factory.ExpireHoldsAsync(_tenantId, expired.Id);
        // Đơn quá hạn của một tenant khác, cùng một lần chạy.
        var otherTenant = Guid.NewGuid();
        var otherBranch = await factory.SeedBranchAsync(otherTenant);
        var otherSku = await factory.SeedSkuAsync(otherTenant, "AO-01");
        await factory.PostLedgerAsync(otherTenant, otherBranch, In(otherSku, 5));
        var otherExpired = await factory.CreateClient(Roles.Staff, otherTenant).CreateOrderAsync(otherBranch, new { skuId = otherSku, quantity = 4 });
        await factory.ExpireHoldsAsync(otherTenant, otherExpired.Id);
        var ledgerBefore = (await factory.LedgerAsync(_tenantId)).Count;

        await Job.RunAsync(default);

        // AC-1: đơn sang Cancelled lý do Expired, phần giữ được giải phóng, on_hand và sổ không đổi.
        var orders = (await factory.OrdersAsync(_tenantId)).ToDictionary(order => order.Id);
        Assert.Equal((OrderStatus.Cancelled, (OrderCancelReason?)OrderCancelReason.Expired), (orders[expired.Id].Status, orders[expired.Id].CancelReason));
        Assert.NotNull(orders[expired.Id].CancelledAt);
        Assert.Equal((OrderStatus.Reserved, (OrderCancelReason?)null), (orders[fresh.Id].Status, orders[fresh.Id].CancelReason));
        // AC-3: job bỏ qua đơn đã duyệt.
        Assert.Equal((OrderStatus.Confirmed, (OrderCancelReason?)null), (orders[confirmed.Id].Status, orders[confirmed.Id].CancelReason));
        Assert.Equal((9, 3, 6), await factory.StockAsync(_tenantId, branch, shirt));
        Assert.Equal((10, 0, 10), await factory.StockAsync(_tenantId, branch, trousers));
        Assert.Equal(ledgerBefore, (await factory.LedgerAsync(_tenantId)).Count);
        var holds = await factory.ReservationsAsync(_tenantId);
        Assert.All(holds.Where(hold => hold.OrderId == expired.Id), hold => Assert.Equal(ReservationStatus.Released, hold.Status));
        Assert.Equal(ReservationStatus.Active, Assert.Single(holds, hold => hold.OrderId == fresh.Id).Status);
        var cancelled = Assert.Single(await factory.OutboxAsync<OrderCancelled>(_tenantId));
        Assert.Equal((expired.Id, "Admin", branch, "Expired"), (cancelled.OrderId, cancelled.Channel, cancelled.BranchId, cancelled.Reason));
        // AC-5: đơn của tenant kia được hủy trong tenant context của chính nó, thông điệp mang đúng tenant đó.
        Assert.Equal(OrderStatus.Cancelled, Assert.Single(await factory.OrdersAsync(otherTenant)).Status);
        Assert.Equal((5, 0, 5), await factory.StockAsync(otherTenant, otherBranch, otherSku));
        Assert.Equal(otherExpired.Id, Assert.Single(await factory.OutboxAsync<OrderCancelled>(otherTenant)).OrderId);

        // AC-2, T06: job chạy lại không tác động lần hai.
        var outboxAfterFirst = await factory.OutboxRowCountAsync(_tenantId);
        var versionAfterFirst = (await factory.BalanceAsync(_tenantId, branch, shirt))!.Version;
        await Job.RunAsync(default);

        Assert.Equal((9, 3, 6), await factory.StockAsync(_tenantId, branch, shirt));
        Assert.Equal(versionAfterFirst, (await factory.BalanceAsync(_tenantId, branch, shirt))!.Version);
        Assert.Equal(outboxAfterFirst, await factory.OutboxRowCountAsync(_tenantId));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
        await factory.AssertInventoryInvariantsAsync(otherTenant);
    }

    [Fact]
    [Trait("UseCase", "UC-ORD-05 AC-4")]
    public async Task ExpireReservations_OneOrderFails_TheOthersAreStillCancelledAndTheFailedOneIsLeftForTheNextRun()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(sku, 10));
        var orders = new List<Guid>();
        foreach (var quantity in new[] { 1, 2, 3 })
        {
            orders.Add((await Staff.CreateOrderAsync(branch, new { skuId = sku, quantity })).Id);
            await factory.ExpireHoldsAsync(_tenantId, orders[^1]);
        }

        var failing = orders[1];
        // Một Api giống hệt, chỉ khác là bước ghi outbox OrderCancelled của đúng một đơn ném lỗi.
        await using var faulty = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            var real = services.Single(service => service.ServiceType == typeof(IEventPublisher)).ImplementationType!;
            services.AddScoped(real);
            services.AddScoped<IEventPublisher>(provider =>
                new FailingPublisher((IEventPublisher)provider.GetRequiredService(real), failing));
        }));

        await faulty.Services.GetRequiredService<ExpireReservationsJob>().RunAsync(default);

        // Đơn lỗi rollback trọn: vẫn Reserved, phần giữ vẫn Active. Hai đơn kia đã được hủy, mỗi đơn một transaction.
        var stored = (await factory.OrdersAsync(_tenantId)).ToDictionary(order => order.Id, order => order.Status);
        Assert.Equal(
            [OrderStatus.Cancelled, OrderStatus.Reserved, OrderStatus.Cancelled],
            orders.Select(id => stored[id]));
        Assert.Equal((10, 2, 8), await factory.StockAsync(_tenantId, branch, sku));
        Assert.Equal(ReservationStatus.Active, Assert.Single(await factory.ReservationsAsync(_tenantId), hold => hold.OrderId == failing).Status);
        Assert.DoesNotContain(await factory.OutboxAsync<OrderCancelled>(_tenantId), message => message.OrderId == failing);
        await factory.AssertInventoryInvariantsAsync(_tenantId);

        // Lần chạy sau, không còn lỗi, hủy nốt đơn đó.
        await Job.RunAsync(default);

        Assert.All(await factory.OrdersAsync(_tenantId), order => Assert.Equal(OrderStatus.Cancelled, order.Status));
        Assert.Equal((10, 0, 10), await factory.StockAsync(_tenantId, branch, sku));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    // Job chạy thật: Hangfire lưu lịch ở schema hangfire_core, dựng job từ DI và gọi nó. Test này chờ đồng hồ thật
    // (docs/testing/strategy.md mục "Quy tắc").
    [Fact]
    [Trait("UseCase", "UC-ORD-05 AC-1")]
    public async Task ExpireReservations_TriggeredThroughHangfire_IsScheduledEveryMinuteAndCancelsTheExpiredOrder()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(sku, 5));
        var order = await Staff.CreateOrderAsync(branch, new { skuId = sku, quantity = 2 });
        await factory.ExpireHoldsAsync(_tenantId, order.Id);
        await using var scheduled = factory.WithWebHostBuilder(builder => builder.UseSetting("Jobs:Enabled", "true"));

        using (var storage = scheduled.Services.GetRequiredService<JobStorage>().GetConnection())
        {
            var recurring = Assert.Single(storage.GetRecurringJobs());
            Assert.Equal((ExpireReservationsJob.Id, Cron.Minutely()), (recurring.Id, recurring.Cron));
        }

        scheduled.Services.GetRequiredService<IRecurringJobManager>().Trigger(ExpireReservationsJob.Id);
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while ((await factory.OrdersAsync(_tenantId)).Single().Status != OrderStatus.Cancelled && DateTime.UtcNow < deadline)
            await Task.Delay(500);

        var cancelled = Assert.Single(await factory.OrdersAsync(_tenantId));
        Assert.Equal((OrderStatus.Cancelled, (OrderCancelReason?)OrderCancelReason.Expired), (cancelled.Status, cancelled.CancelReason));
        Assert.Equal((5, 0, 5), await factory.StockAsync(_tenantId, branch, sku));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    private sealed class FailingPublisher(IEventPublisher inner, Guid failingOrderId) : IEventPublisher
    {
        public void Enqueue<TPayload>(TPayload payload)
            where TPayload : notnull
        {
            if (payload is OrderCancelled cancelled && cancelled.OrderId == failingOrderId)
                throw new InvalidOperationException("Injected outbox failure.");

            inner.Enqueue(payload);
        }
    }
}
