using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Auth;
using Oism.Contracts;
using Oism.Core.Application.Orders;
using Oism.Core.Application.Orders.SearchPosSkus;
using Oism.Core.Domain.Inventory;
using Oism.Core.Domain.Orders;
using static Oism.Core.IntegrationTests.TestKit;

namespace Oism.Core.IntegrationTests.Orders;

// W3-02: POS checkout trong một transaction, Idempotency-Key, và tìm SKU kèm tồn khả dụng
// (UC-POS-01, UC-POS-02; FR-POS-02, FR-POS-04, NFR-SEC-02).
public sealed class PosCheckoutTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private HttpClient Owner => factory.CreateClient(Roles.Owner, _tenantId);

    [Fact]
    [Trait("UseCase", "UC-POS-02 AC-1")]
    public async Task Checkout_ValidCart_SavesACompletedOrderWithStockCostLedgerAndPaymentInOneGo()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var shirt = await factory.SeedSkuAsync(_tenantId, "AO-01", "Áo thun, Đen", retailPrice: 150_000);
        var trousers = await factory.SeedSkuAsync(_tenantId, "QUAN-01", "Quần kaki", retailPrice: 80_000);
        await factory.PostLedgerAsync(_tenantId, branch, In(shirt, 5, 110_000), In(trousers, 5, 40_000));
        var cashier = factory.CreateClient(Roles.Cashier, _tenantId, branch);

        var response = await CheckoutAsync(cashier, Guid.NewGuid(), branch, "Cash", 400_000,
            new { skuId = shirt, quantity = 2 }, new { skuId = trousers, quantity = 1, unitPrice = 70_000, discount = 5_000 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var order = json.Deserialize<OrderDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(("POS", "Completed", branch, 365_000m), (order.Channel, order.Status, order.BranchId, order.TotalAmount));
        Assert.NotNull(order.ConfirmedAt);
        Assert.Equal(order.ConfirmedAt, order.CompletedAt);
        Assert.NotNull(order.CreatedBy);
        Assert.Equal(
            new[] { (shirt, "AO-01", 2, 150_000m, 0m), (trousers, "QUAN-01", 1, 70_000m, 5_000m) }.Order(),
            order.Items.Select(item => (item.SkuId, item.SkuCode, item.Quantity, item.UnitPrice, item.Discount)).Order());
        // Thanh toán được ghi kèm người xác nhận là thu ngân.
        Assert.Equal(("Cash", 400_000m, order.CreatedBy), (order.Payment!.Method, order.Payment.Amount, (Guid?)order.Payment.ConfirmedBy));
        // Cashier không thấy giá vốn.
        Assert.False(json.GetProperty("items")[0].TryGetProperty("costPrice", out _));

        // on_hand giảm, reserved về như cũ; giá vốn được chốt; sổ có dòng OUT lý do Sale cho mỗi dòng đơn.
        Assert.Equal((3, 0, 3), await factory.StockAsync(_tenantId, branch, shirt));
        Assert.Equal((4, 0, 4), await factory.StockAsync(_tenantId, branch, trousers));
        var stored = Assert.Single(await factory.OrdersAsync(_tenantId));
        Assert.Equal((OrderChannel.POS, OrderStatus.Completed), (stored.Channel, stored.Status));
        Assert.NotNull(stored.IdempotencyKey);
        Assert.Equal(
            new[] { (shirt, (decimal?)110_000m), (trousers, (decimal?)40_000m) }.Order(),
            stored.Items.Select(item => (item.SkuId, item.CostPrice)).Order());
        var sales = (await factory.LedgerAsync(_tenantId)).Where(line => line.Reason == LedgerReason.Sale).ToList();
        Assert.Equal(
            new[] { (shirt, LedgerType.OUT, 2, 3, 110_000m), (trousers, LedgerType.OUT, 1, 4, 40_000m) }.Order(),
            sales.Select(line => (line.SkuId, line.Type, line.Quantity, line.BalanceAfter, line.UnitCost)).Order());
        Assert.All(sales, line => Assert.Equal((LedgerReference.Order, order.Id, order.CreatedBy), (line.ReferenceType, line.ReferenceId, line.CreatedBy)));
        Assert.All(await factory.ReservationsAsync(_tenantId), hold => Assert.Equal(ReservationStatus.Consumed, hold.Status));
        var payment = Assert.Single(await factory.QueryAsync(_tenantId, db => db.Payments.AsNoTracking().ToListAsync()));
        Assert.Equal((_tenantId, order.Id, PaymentMethod.Cash, 400_000m), (payment.TenantId, payment.OrderId, payment.Method, payment.Amount));

        // Đơn POS phát OrderConfirmed và StockChanged, không phát OrderReserved.
        var confirmed = Assert.Single(await factory.OutboxAsync<OrderConfirmed>(_tenantId));
        Assert.Equal((order.Id, "POS", branch), (confirmed.OrderId, confirmed.Channel, confirmed.BranchId));
        Assert.Equal([40_000m, 110_000m], confirmed.Lines.Select(line => line.CostPrice).Order());
        Assert.Empty(await factory.OutboxAsync<OrderReserved>(_tenantId));
        Assert.Contains(
            await factory.OutboxAsync<StockChanged>(_tenantId),
            change => (change.SkuId, change.OnHand, change.Reserved, change.Available) == (shirt, 3, 0, 3));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("UseCase", "UC-POS-02 AC-1")]
    public async Task Checkout_ByOwnerPayingByQr_ReturnsTheCostPriceAndRecordsTheMethod()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01", retailPrice: 150_000);
        await factory.PostLedgerAsync(_tenantId, branch, In(sku, 5, 110_000));

        var response = await CheckoutAsync(Owner, Guid.NewGuid(), branch, "QR", 150_000, new { skuId = sku, quantity = 1 });

        var order = (await response.Content.ReadFromJsonAsync<OrderDto>())!;
        Assert.Equal(("Completed", "QR", 150_000m), (order.Status, order.Payment!.Method, order.Payment.Amount));
        Assert.Equal(110_000m, Assert.Single(order.Items).CostPrice);
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("UseCase", "UC-POS-02 AC-2")]
    [Trait("UseCase", "UC-POS-02 AC-5")]
    public async Task Checkout_StockGoneOrHeldForAnOnlineOrder_Returns409WithSkuAndAvailableAndSavesNothing()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var shirt = await factory.SeedSkuAsync(_tenantId, "AO-01");
        var trousers = await factory.SeedSkuAsync(_tenantId, "QUAN-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(shirt, 5), In(trousers, 3));
        // Đơn online đang giữ 2 trong 3 quần: POS chỉ còn bán được 1.
        await factory.SubmitOrderAsync(_tenantId, Online("SP-1", branch, new SubmitOrderLine("QUAN-01", 2, 80_000, 0)));
        var ledgerBefore = (await factory.LedgerAsync(_tenantId)).Count;
        var outboxBefore = await factory.OutboxRowCountAsync(_tenantId);

        var response = await CheckoutAsync(
            Owner, Guid.NewGuid(), branch, "Cash", 1_000_000, new { skuId = shirt, quantity = 1 }, new { skuId = trousers, quantity = 2 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("insufficient_stock", problem.GetProperty("code").GetString());
        var detail = Assert.Single(problem.GetProperty("details").EnumerateArray());
        Assert.Equal(
            (trousers, 2, 1),
            (detail.GetProperty("skuId").GetGuid(), detail.GetProperty("requested").GetInt32(), detail.GetProperty("available").GetInt32()));
        // Không gì được lưu: chỉ còn đơn online, không thanh toán, không dòng sổ hay thông điệp mới.
        Assert.Equal(OrderChannel.Shopee, Assert.Single(await factory.OrdersAsync(_tenantId)).Channel);
        Assert.Equal(0, await factory.QueryAsync(_tenantId, db => db.Payments.CountAsync()));
        Assert.Equal((5, 0, 5), await factory.StockAsync(_tenantId, branch, shirt));
        Assert.Equal((3, 2, 1), await factory.StockAsync(_tenantId, branch, trousers));
        Assert.Equal(ledgerBefore, (await factory.LedgerAsync(_tenantId)).Count);
        Assert.Equal(outboxBefore, await factory.OutboxRowCountAsync(_tenantId));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("Scenario", "T03")]
    [Trait("UseCase", "UC-POS-02 AC-3")]
    public async Task Checkout_SameIdempotencyKeyTwice_KeepsOneOrderAndReturnsItAgain()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01", retailPrice: 150_000);
        await factory.PostLedgerAsync(_tenantId, branch, In(sku, 5));
        var cashier = factory.CreateClient(Roles.Cashier, _tenantId, branch);
        var key = Guid.NewGuid();

        var first = await CheckoutAsync(cashier, key, branch, "Cash", 150_000, new { skuId = sku, quantity = 1 });
        var ledgerAfterFirst = (await factory.LedgerAsync(_tenantId)).Count;
        var outboxAfterFirst = await factory.OutboxRowCountAsync(_tenantId);
        // Lần gửi lại mang giỏ khác vẫn nhận lại đơn cũ: khóa quyết định, không phải nội dung.
        var second = await CheckoutAsync(cashier, key, branch, "Cash", 300_000, new { skuId = sku, quantity = 2 });
        var ledgerAfterSecond = (await factory.LedgerAsync(_tenantId)).Count;
        var outboxAfterSecond = await factory.OutboxRowCountAsync(_tenantId);
        // Khóa khác là một lần bấm thanh toán khác.
        var other = await CheckoutAsync(cashier, Guid.NewGuid(), branch, "Cash", 150_000, new { skuId = sku, quantity = 1 });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.Created, other.StatusCode);
        var (created, replayed) = ((await first.Content.ReadFromJsonAsync<OrderDto>())!, (await second.Content.ReadFromJsonAsync<OrderDto>())!);
        Assert.Equal(
            (created.Id, created.OrderNumber, "Completed", 150_000m, created.Payment),
            (replayed.Id, replayed.OrderNumber, replayed.Status, replayed.TotalAmount, replayed.Payment));
        Assert.Equal(created.Items, replayed.Items);
        // Lần gửi lại không tạo đơn, phần giữ, dòng sổ hay thông điệp nào; chỉ khóa thứ hai mới bán thêm một đơn.
        Assert.Equal((ledgerAfterFirst, outboxAfterFirst), (ledgerAfterSecond, outboxAfterSecond));
        Assert.Equal(2, (await factory.OrdersAsync(_tenantId)).Count);
        Assert.Equal(2, (await factory.ReservationsAsync(_tenantId)).Count);
        Assert.Equal((3, 0, 3), await factory.StockAsync(_tenantId, branch, sku));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("Scenario", "T05")]
    [Trait("UseCase", "UC-POS-02 AC-6")]
    public async Task Checkout_LedgerWriteFails_RollsBackTheOrderTheHoldsAndThePayment()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01", retailPrice: 150_000);
        await factory.PostLedgerAsync(_tenantId, branch, In(sku, 5));
        var before = (await factory.BalanceAsync(_tenantId, branch, sku))!;
        var outboxBefore = await factory.OutboxRowCountAsync(_tenantId);
        var key = Guid.NewGuid();

        // Lỗi xảy ra sau khi đơn và phần giữ đã được ghi xuống database trong transaction: rollback phải gỡ cả hai.
        var response = await CheckoutAsync(
            factory.CreateClientWithFailingLedger(Owner), key, branch, "Cash", 150_000, new { skuId = sku, quantity = 1 });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(await factory.OrdersAsync(_tenantId));
        Assert.Empty(await factory.ReservationsAsync(_tenantId));
        Assert.Equal(0, await factory.QueryAsync(_tenantId, db => db.Payments.CountAsync()));
        var after = (await factory.BalanceAsync(_tenantId, branch, sku))!;
        Assert.Equal((before.OnHand, before.Reserved, before.Version), (after.OnHand, after.Reserved, after.Version));
        Assert.Single(await factory.LedgerAsync(_tenantId));
        Assert.Equal(outboxBefore, await factory.OutboxRowCountAsync(_tenantId));

        // Thử lại với cùng khóa sau khi hết lỗi: bán được, vì lần trước không để lại gì.
        Assert.Equal(HttpStatusCode.Created, (await CheckoutAsync(Owner, key, branch, "Cash", 150_000, new { skuId = sku, quantity = 1 })).StatusCode);
        Assert.Equal((4, 0, 4), await factory.StockAsync(_tenantId, branch, sku));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("UseCase", "UC-POS-02 AC-7")]
    [Trait("UseCase", "UC-AUTH-03 AC-4")]
    public async Task Checkout_CashierAtAnotherBranchOrStaff_Returns403AndSavesNothing()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var otherBranch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01", retailPrice: 150_000);
        await factory.PostLedgerAsync(_tenantId, otherBranch, In(sku, 5));
        var cashier = factory.CreateClient(Roles.Cashier, _tenantId, branch);

        var atOtherBranch = await CheckoutAsync(cashier, Guid.NewGuid(), otherBranch, "Cash", 150_000, new { skuId = sku, quantity = 1 });
        // Cashier không gắn chi nhánh nào thì không bán được ở đâu.
        var withoutBranch = await CheckoutAsync(
            factory.CreateClient(Roles.Cashier, _tenantId), Guid.NewGuid(), otherBranch, "Cash", 150_000, new { skuId = sku, quantity = 1 });
        var asStaff = await CheckoutAsync(
            factory.CreateClient(Roles.Staff, _tenantId), Guid.NewGuid(), otherBranch, "Cash", 150_000, new { skuId = sku, quantity = 1 });
        var searchAtOtherBranch = await cashier.GetAsync($"/pos/skus?branchId={otherBranch}&query=AO");
        var anonymous = await factory.CreateClient().GetAsync($"/pos/skus?branchId={branch}");

        await atOtherBranch.AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        await withoutBranch.AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        await asStaff.AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        await searchAtOtherBranch.AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        await anonymous.AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthenticated");
        Assert.Empty(await factory.OrdersAsync(_tenantId));
        Assert.Equal((5, 0, 5), await factory.StockAsync(_tenantId, otherBranch, sku));
    }

    [Fact]
    [Trait("UseCase", "UC-POS-02 AC-1")]
    public async Task Checkout_InvalidInput_Returns400AndSavesNothing()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01", retailPrice: 150_000);
        await factory.PostLedgerAsync(_tenantId, branch, In(sku, 5));
        var item = new { skuId = sku, quantity = 1 };
        var owner = Owner;

        HttpResponseMessage[] responses =
        [
            // Thiếu header Idempotency-Key, hoặc header không phải uuid.
            await owner.PostAsJsonAsync("/pos/checkout", new { branchId = branch, items = new[] { item }, payment = new { method = "Cash", amount = 150_000 } }),
            await CheckoutAsync(owner, "khong-phai-uuid", branch, "Cash", 150_000, item),
            await CheckoutAsync(owner, Guid.NewGuid(), branch, "Cash", 150_000),
            await CheckoutAsync(owner, Guid.NewGuid(), branch, "Cash", 150_000, new { skuId = sku, quantity = 0 }),
            await CheckoutAsync(owner, Guid.NewGuid(), branch, "Card", 150_000, item),
            // Số tiền thanh toán nhỏ hơn tổng đơn.
            await CheckoutAsync(owner, Guid.NewGuid(), branch, "Cash", 149_999, item),
            await owner.SendAsync(Request(Guid.NewGuid(), new { branchId = branch, items = new[] { item } })),
            await CheckoutAsync(owner, Guid.NewGuid(), Guid.Empty, "Cash", 150_000, item),
        ];
        var unknownBranch = await CheckoutAsync(owner, Guid.NewGuid(), Guid.NewGuid(), "Cash", 150_000, item);

        foreach (var response in responses)
            await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await unknownBranch.AssertProblemAsync(HttpStatusCode.Conflict, "reference_not_ready");
        Assert.Empty(await factory.OrdersAsync(_tenantId));
        Assert.Equal((5, 0, 5), await factory.StockAsync(_tenantId, branch, sku));
    }

    [Fact]
    [Trait("UseCase", "UC-POS-01 AC-1")]
    [Trait("UseCase", "UC-POS-01 AC-3")]
    [Trait("UseCase", "UC-POS-01 AC-5")]
    public async Task SearchSkus_ByPartOfNameCodeOrBarcode_ReturnsActiveSkusWithRetailPriceAndAvailableAtTheBranch()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var otherBranch = await factory.SeedBranchAsync(_tenantId);
        var shirt = await factory.SeedSkuAsync(_tenantId, "AO-01", "Áo thun, Đen", 150_000, true, "8930000000017", "AO01-CODE128");
        var trousers = await factory.SeedSkuAsync(_tenantId, "QUAN-01", "Quần kaki", 80_000, true, "8930000000024");
        var hat = await factory.SeedSkuAsync(_tenantId, "MU-01", "Mũ lưỡi trai", 50_000);
        await factory.SeedSkuAsync(_tenantId, "AO-99", "Áo thun ngừng bán", 1, isActive: false);
        // SKU cùng mã ở tenant khác không lọt vào kết quả.
        await factory.SeedSkuAsync(Guid.NewGuid(), "AO-01", "Áo của tenant khác");
        await factory.PostLedgerAsync(_tenantId, branch, In(shirt, 5), In(trousers, 2));
        await factory.PostLedgerAsync(_tenantId, otherBranch, In(shirt, 40), In(hat, 7));
        await Owner.CreateOrderAsync(branch, new { skuId = shirt, quantity = 2 });
        var cashier = factory.CreateClient(Roles.Cashier, _tenantId, branch);

        var byName = await SearchAsync(cashier, branch, "áo THUN");
        var byCode = await SearchAsync(cashier, branch, "quan-");
        var byBarcode = await SearchAsync(cashier, branch, "00000002");
        var everything = await SearchAsync(factory.CreateClient(Roles.Staff, _tenantId), branch, null);
        var nothing = await SearchAsync(cashier, branch, "khong-co");

        // Tồn khả dụng là của chi nhánh đang bán, đã trừ phần đang giữ; SKU ngừng bán không hiện.
        var found = Assert.Single(byName);
        Assert.Equal((shirt, "AO-01", "Áo thun, Đen", 150_000m, 3), (found.SkuId, found.SkuCode, found.Name, found.RetailPrice, found.Available));
        Assert.Equal(["8930000000017", "AO01-CODE128"], found.Barcodes);
        Assert.Equal((trousers, 2), (Assert.Single(byCode).SkuId, byCode[0].Available));
        Assert.Equal(trousers, Assert.Single(byBarcode).SkuId);
        // AC-3: SKU hết hàng ở chi nhánh vẫn hiện, với tồn khả dụng 0.
        Assert.Equal(
            [("AO-01", 3), ("MU-01", 0), ("QUAN-01", 2)],
            everything.Select(sku => (sku.SkuCode, sku.Available)));
        Assert.Empty(nothing);
    }

    [Fact]
    [Trait("UseCase", "UC-POS-01 AC-2")]
    public async Task SearchSkus_ManyPartialMatches_ReturnsAtMostTwentyWithTheExactCodeOrBarcodeFirst()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        for (var index = 10; index < 35; index++)
            await factory.SeedSkuAsync(_tenantId, $"AO-{index}", "Áo thun", 150_000, true, $"89300000{index}");
        var scanned = await factory.SeedSkuAsync(_tenantId, "ZZ-AO", "Áo khoác", 300_000, true, "AO-1");
        var owner = Owner;

        var page = await SearchAsync(owner, branch, "AO-1");
        var byExactCode = await SearchAsync(owner, branch, "ao-34");
        var missingBranch = await owner.GetAsync("/pos/skus?query=AO");

        // "AO-1" khớp một phần mười mã SKU và khớp đúng một mã vạch: SKU có mã vạch đó đứng đầu dù mã của nó xếp cuối.
        Assert.Equal(scanned, page[0].SkuId);
        Assert.Equal(11, page.Count);
        Assert.Equal("AO-34", Assert.Single(byExactCode).SkuCode);
        Assert.Equal(20, (await SearchAsync(owner, branch, "AO")).Count);
        await missingBranch.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
    }

    private static async Task<List<PosSkuDto>> SearchAsync(HttpClient client, Guid branchId, string? query) =>
        (await client.GetFromJsonAsync<List<PosSkuDto>>($"/pos/skus?branchId={branchId}&query={Uri.EscapeDataString(query ?? string.Empty)}"))!;

    private static Task<HttpResponseMessage> CheckoutAsync(
        HttpClient client, object idempotencyKey, Guid branchId, string method, decimal amount, params object[] items) =>
        client.SendAsync(Request(idempotencyKey, new { branchId, items, payment = new { method, amount } }));

    private static HttpRequestMessage Request(object idempotencyKey, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/pos/checkout") { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", idempotencyKey.ToString());
        return request;
    }
}
