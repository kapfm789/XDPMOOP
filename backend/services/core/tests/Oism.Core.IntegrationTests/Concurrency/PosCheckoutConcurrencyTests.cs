using System.Net;
using System.Net.Http.Json;
using Oism.BuildingBlocks.Auth;
using Oism.Contracts;
using Oism.Core.Application.Orders;
using Oism.Core.Domain.Inventory;
using Oism.Core.Domain.Orders;
using static Oism.Core.IntegrationTests.TestKit;

namespace Oism.Core.IntegrationTests.Concurrency;

// W3-01, W3-02: POS, đơn online, duyệt và hủy tranh nhau trên cùng một dòng số dư hoặc cùng một đơn.
// Mọi request được giữ sau một cổng rồi thả cùng lúc.
public sealed class PosCheckoutConcurrencyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Race không lộ ra ở mọi lần chạy: lặp 20 lần (docs/testing/strategy.md mục "Các tầng test").
    public static IEnumerable<object[]> Rounds => Enumerable.Range(1, 20).Select(round => new object[] { round });

    [Theory]
    [MemberData(nameof(Rounds))]
    [Trait("Scenario", "T02")]
    [Trait("UseCase", "UC-POS-02 AC-4")]
    public async Task Checkout_PosAndAnOnlineOrderForTheLastUnit_ExactlyOneGetsIt(int round)
    {
        var tenantId = Guid.NewGuid();
        var branch = await factory.SeedBranchAsync(tenantId);
        var sku = await factory.SeedSkuAsync(tenantId, "AO-01", retailPrice: 150_000);
        await factory.PostLedgerAsync(tenantId, branch, In(sku, 1));
        var cashier = factory.CreateClient(Roles.Cashier, tenantId, branch);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var checkout = Task.Run(async () =>
        {
            await gate.Task;
            return await cashier.SendAsync(Checkout(Guid.NewGuid(), branch, sku, quantity: 1, amount: 150_000));
        });
        var online = Task.Run(async () =>
        {
            await gate.Task;
            await factory.SubmitOrderAsync(tenantId, Online($"T02-{round}", branch, new SubmitOrderLine("AO-01", 1, 150_000, 0)));
        });
        gate.SetResult();
        await online;
        var response = await checkout;

        // Đúng một bên thành công: hoặc POS bán được và đơn online bị từ chối, hoặc ngược lại.
        var order = Assert.Single(await factory.OrdersAsync(tenantId));
        var rejected = await factory.OutboxAsync<OrderRejected>(tenantId);
        if (response.StatusCode == HttpStatusCode.Created)
        {
            Assert.Equal((OrderChannel.POS, OrderStatus.Completed), (order.Channel, order.Status));
            Assert.Equal("InsufficientStock", Assert.Single(rejected).Reason);
            Assert.Equal((0, 0, 0), await factory.StockAsync(tenantId, branch, sku));
        }
        else
        {
            await response.AssertProblemAsync(HttpStatusCode.Conflict, "insufficient_stock");
            Assert.Equal((OrderChannel.Shopee, OrderStatus.Reserved), (order.Channel, order.Status));
            Assert.Empty(rejected);
            Assert.Equal((1, 1, 0), await factory.StockAsync(tenantId, branch, sku));
        }

        await factory.AssertInventoryInvariantsAsync(tenantId);
    }

    // Hai quầy cùng bán đơn vị cuối, và mạng gửi lại cùng một lần bấm thanh toán nhiều lần cùng lúc.
    [Fact]
    [Trait("Scenario", "T03")]
    [Trait("UseCase", "UC-POS-02 AC-3")]
    [Trait("UseCase", "UC-POS-02 AC-4")]
    public async Task Checkout_SameKeySentTenTimesAtOnceAndManyTillsForTheLastUnits_OneOrderPerKeyAndNoOversell()
    {
        var tenantId = Guid.NewGuid();
        var branch = await factory.SeedBranchAsync(tenantId);
        var sku = await factory.SeedSkuAsync(tenantId, "AO-01", retailPrice: 150_000);
        await factory.PostLedgerAsync(tenantId, branch, In(sku, 3));
        var cashier = factory.CreateClient(Roles.Cashier, tenantId, branch);
        var repeatedKey = Guid.NewGuid();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var repeated = Enumerable.Range(0, 10).Select(async _ =>
        {
            await gate.Task;
            return await cashier.SendAsync(Checkout(repeatedKey, branch, sku, quantity: 1, amount: 150_000));
        }).ToArray();
        var tills = Enumerable.Range(0, 20).Select(async _ =>
        {
            await gate.Task;
            return await cashier.SendAsync(Checkout(Guid.NewGuid(), branch, sku, quantity: 1, amount: 150_000));
        }).ToArray();
        gate.SetResult();
        var repeatedResponses = await Task.WhenAll(repeated);
        var tillResponses = await Task.WhenAll(tills);

        // Cùng khóa: hoặc mọi lần gửi nhận lại đúng một đơn (một lần 201, còn lại 200), hoặc khóa đó thua hết vì hết hàng.
        var accepted = repeatedResponses.Where(response => response.IsSuccessStatusCode).ToList();
        if (accepted.Count > 0)
        {
            Assert.Equal(10, accepted.Count);
            Assert.Single(accepted, response => response.StatusCode == HttpStatusCode.Created);
            var ids = new List<Guid>();
            foreach (var response in accepted)
                ids.Add((await response.Content.ReadFromJsonAsync<OrderDto>())!.Id);
            Assert.Single(ids.Distinct());
        }

        foreach (var refused in repeatedResponses.Concat(tillResponses).Where(response => !response.IsSuccessStatusCode))
            await refused.AssertProblemAsync(HttpStatusCode.Conflict, "insufficient_stock");
        // Ba đơn vị, ba đơn: không bán vượt tồn và không lời gọi nào chết vì deadlock.
        var orders = await factory.OrdersAsync(tenantId);
        Assert.Equal(3, orders.Count);
        Assert.Equal(3, orders.Select(order => order.IdempotencyKey).Distinct().Count());
        Assert.Equal(3, tillResponses.Count(response => response.StatusCode == HttpStatusCode.Created) + (accepted.Count > 0 ? 1 : 0));
        Assert.Equal((0, 0, 0), await factory.StockAsync(tenantId, branch, sku));
        Assert.Equal(3, (await factory.LedgerAsync(tenantId)).Count(line => line.Reason == LedgerReason.Sale));
        await factory.AssertInventoryInvariantsAsync(tenantId);
    }

    // Duyệt và hủy cùng khóa dòng đơn nên chỉ một bên thắng. Job hết hạn (W3-04) đi qua đúng handler hủy này,
    // nên đây cũng là phần của T07 làm được trước khi có job.
    [Theory]
    [MemberData(nameof(Rounds))]
    [Trait("UseCase", "UC-ORD-03 AC-5")]
    public async Task ConfirmAndCancel_SameOrderAtTheSameTime_ExactlyOneWins(int round)
    {
        var tenantId = Guid.NewGuid();
        var branch = await factory.SeedBranchAsync(tenantId);
        var sku = await factory.SeedSkuAsync(tenantId, $"AO-{round}");
        await factory.PostLedgerAsync(tenantId, branch, In(sku, 5));
        var staff = factory.CreateClient(Roles.Staff, tenantId);
        var order = await staff.CreateOrderAsync(branch, new { skuId = sku, quantity = 2 });
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = new[] { "confirm", "cancel" }.Select(async action =>
        {
            await gate.Task;
            return await staff.PostAsync($"/orders/{order.Id}/{action}", null);
        }).ToArray();
        gate.SetResult();
        var responses = await Task.WhenAll(tasks);

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        await responses.Single(response => response.StatusCode != HttpStatusCode.OK)
            .AssertProblemAsync(HttpStatusCode.Conflict, "invalid_state_transition");
        // Không có chuyện vừa xuất hàng vừa giải phóng phần giữ.
        var stored = Assert.Single(await factory.OrdersAsync(tenantId));
        var hold = Assert.Single(await factory.ReservationsAsync(tenantId));
        var sales = (await factory.LedgerAsync(tenantId)).Count(line => line.Reason == LedgerReason.Sale);
        if (stored.Status == OrderStatus.Confirmed)
        {
            Assert.Equal((ReservationStatus.Consumed, 1), (hold.Status, sales));
            Assert.Equal((3, 0, 3), await factory.StockAsync(tenantId, branch, sku));
        }
        else
        {
            Assert.Equal((OrderStatus.Cancelled, ReservationStatus.Released, 0), (stored.Status, hold.Status, sales));
            Assert.Equal((5, 0, 5), await factory.StockAsync(tenantId, branch, sku));
        }

        await factory.AssertInventoryInvariantsAsync(tenantId);
    }

    private static HttpRequestMessage Checkout(Guid idempotencyKey, Guid branchId, Guid skuId, int quantity, decimal amount)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/pos/checkout")
        {
            Content = JsonContent.Create(new
            {
                branchId,
                items = new[] { new { skuId, quantity } },
                payment = new { method = "Cash", amount },
            }),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey.ToString());
        return request;
    }
}
