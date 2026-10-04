using System.Net;
using System.Net.Http.Json;
using Oism.BuildingBlocks.Auth;
using Oism.Contracts;
using Oism.Core.Domain.Orders;
using static Oism.Core.IntegrationTests.TestKit;

namespace Oism.Core.IntegrationTests.Concurrency;

// W2-05: 50 request đồng thời cho 1 sản phẩm, đúng 1 thành công (FR-RSE-02, NFR-PERF-02).
// Mọi request được giữ sau một cổng rồi thả cùng lúc, để chúng tranh nhau ở khóa dòng số dư.
public sealed class ReserveStockConcurrencyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Race không lộ ra ở mọi lần chạy: lặp 20 lần (docs/testing/strategy.md mục "Các tầng test").
    public static IEnumerable<object[]> Rounds => Enumerable.Range(1, 20).Select(round => new object[] { round });

    [Theory]
    [MemberData(nameof(Rounds))]
    [Trait("Scenario", "T01")]
    [Trait("UseCase", "UC-ORD-01 AC-5")]
    public async Task SubmitOrder_FiftyOnlineOrdersForTheLastUnit_ExactlyOneIsReserved(int round)
    {
        var tenantId = Guid.NewGuid();
        var branch = await factory.SeedBranchAsync(tenantId);
        var sku = await factory.SeedSkuAsync(tenantId, "AO-01");
        await factory.PostLedgerAsync(tenantId, branch, In(sku, 1));
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = Enumerable.Range(0, 50).Select(async index =>
        {
            await gate.Task;
            await factory.SubmitOrderAsync(tenantId, Online($"T01-{round}-{index}", branch, new SubmitOrderLine("AO-01", 1, 150_000, 0)));
        }).ToArray();
        gate.SetResult();
        await Task.WhenAll(tasks);

        Assert.Equal(OrderStatus.Reserved, Assert.Single(await factory.OrdersAsync(tenantId)).Status);
        var balance = (await factory.BalanceAsync(tenantId, branch, sku))!;
        Assert.Equal((1, 1, 0), (balance.OnHand, balance.Reserved, balance.Available));
        Assert.Equal(ReservationStatus.Active, Assert.Single(await factory.ReservationsAsync(tenantId)).Status);
        // 49 đơn thua bị từ chối có kiểm soát: mỗi đơn một OrderRejected nêu SKU thiếu, không đơn nào lỗi hệ thống.
        Assert.Single(await factory.OutboxAsync<OrderReserved>(tenantId));
        var rejected = await factory.OutboxAsync<OrderRejected>(tenantId);
        Assert.Equal(49, rejected.Count);
        Assert.All(rejected, message => Assert.Equal(
            ("InsufficientStock", new OrderRejectedDetail("AO-01", Requested: 1, Available: 0)), (message.Reason, Assert.Single(message.Details))));
        await factory.AssertInventoryInvariantsAsync(tenantId);
    }

    // Cùng kịch bản qua API tạo đơn thủ công: 1 lời gọi nhận 201, 49 lời gọi nhận 409 insufficient_stock.
    [Fact]
    [Trait("Scenario", "T01")]
    [Trait("UseCase", "UC-ORD-02 AC-2")]
    public async Task CreateOrder_FiftyRequestsForTheLastUnit_ExactlyOneGets201()
    {
        var tenantId = Guid.NewGuid();
        var branch = await factory.SeedBranchAsync(tenantId);
        var sku = await factory.SeedSkuAsync(tenantId, "AO-01");
        await factory.PostLedgerAsync(tenantId, branch, In(sku, 1));
        var staff = factory.CreateClient(Roles.Staff, tenantId);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = Enumerable.Range(0, 50).Select(async _ =>
        {
            await gate.Task;
            return await staff.PostAsJsonAsync("/orders", new { branchId = branch, items = new[] { new { skuId = sku, quantity = 1 } } });
        }).ToArray();
        gate.SetResult();
        var responses = await Task.WhenAll(tasks);

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        foreach (var refused in responses.Where(response => response.StatusCode != HttpStatusCode.Created))
            await refused.AssertProblemAsync(HttpStatusCode.Conflict, "insufficient_stock");
        Assert.Single(await factory.OrdersAsync(tenantId));
        var balance = (await factory.BalanceAsync(tenantId, branch, sku))!;
        Assert.Equal((1, 1, 0), (balance.OnHand, balance.Reserved, balance.Available));
        await factory.AssertInventoryInvariantsAsync(tenantId);
    }

    // Hai đơn cùng mua hai SKU theo thứ tự dòng ngược nhau: khóa số dư luôn theo sku_id tăng dần nên không chờ nhau vòng tròn.
    [Fact]
    [Trait("UseCase", "UC-ORD-01 AC-5")]
    public async Task CreateOrder_ManyOrdersOverTheSameTwoSkusInOppositeLineOrder_NoDeadlockAndNoOversell()
    {
        var tenantId = Guid.NewGuid();
        var branch = await factory.SeedBranchAsync(tenantId);
        var shirt = await factory.SeedSkuAsync(tenantId, "AO-01");
        var trousers = await factory.SeedSkuAsync(tenantId, "QUAN-01");
        await factory.PostLedgerAsync(tenantId, branch, In(shirt, 10), In(trousers, 10));
        var staff = factory.CreateClient(Roles.Staff, tenantId);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = Enumerable.Range(0, 40).Select(async index =>
        {
            await gate.Task;
            var (first, second) = index % 2 == 0 ? (shirt, trousers) : (trousers, shirt);
            return await staff.PostAsJsonAsync("/orders", new
            {
                branchId = branch,
                items = new[] { new { skuId = first, quantity = 1 }, new { skuId = second, quantity = 1 } },
            });
        }).ToArray();
        gate.SetResult();
        var responses = await Task.WhenAll(tasks);

        // Không lời gọi nào chết vì deadlock (500): chỉ có 201 và 409.
        Assert.Equal(10, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(30, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(10, (await factory.BalanceAsync(tenantId, branch, shirt))!.Reserved);
        Assert.Equal(10, (await factory.BalanceAsync(tenantId, branch, trousers))!.Reserved);
        await factory.AssertInventoryInvariantsAsync(tenantId);
    }
}
