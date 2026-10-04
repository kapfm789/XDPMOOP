using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Auth;
using Oism.Contracts;
using Oism.Core.Application.Orders;
using Oism.Core.Domain.Inventory;
using Oism.Core.Domain.Orders;
using static Oism.Core.IntegrationTests.TestKit;

namespace Oism.Core.IntegrationTests.Orders;

// W2-04, W2-05, W2-06: tạo đơn theo Canonical Order và giữ hàng trong một transaction (UC-ORD-01, UC-ORD-02).
public sealed class ReserveStockTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private HttpClient Staff => factory.CreateClient(Roles.Staff, _tenantId);

    [Fact]
    [Trait("UseCase", "UC-ORD-02 AC-1")]
    [Trait("UseCase", "UC-ORD-02 AC-3")]
    [Trait("UseCase", "UC-ORD-01 AC-2")]
    [Trait("UseCase", "UC-PROD-04 AC-4")]
    public async Task CreateOrder_EnoughStock_ReservesEveryLineWithoutTouchingOnHandOrLedger()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var shirt = await factory.SeedSkuAsync(_tenantId, "AO-01", "Áo thun, Đen", retailPrice: 150_000);
        var trousers = await factory.SeedSkuAsync(_tenantId, "QUAN-01", "Quần kaki", retailPrice: 80_000);
        await factory.PostLedgerAsync(_tenantId, branch, In(shirt, 5), In(trousers, 5));

        var response = await Staff.PostAsJsonAsync("/orders", new
        {
            branchId = branch,
            note = "  Khách gọi điện  ",
            items = new object[]
            {
                // Không nhập đơn giá: lấy giá lẻ hiện tại. Nhập đơn giá khác: dùng giá đã nhập.
                new { skuId = shirt, quantity = 2 },
                new { skuId = trousers, quantity = 1, unitPrice = 70_000, discount = 5_000 },
            },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = (await response.Content.ReadFromJsonAsync<OrderDto>())!;
        Assert.Equal(("Admin", "Reserved", branch, "Khách gọi điện", 365_000m), (order.Channel, order.Status, order.BranchId, order.Note, order.TotalAmount));
        Assert.StartsWith("DH", order.OrderNumber);
        Assert.NotNull(order.CreatedBy);
        Assert.Null(order.ExternalOrderId);
        // Thời hạn giữ hàng mặc định 30 phút (ADR-0005).
        Assert.Equal(order.CreatedAt.AddMinutes(30), order.ReservedUntil);
        Assert.Equal(
            [(shirt, "AO-01", "Áo thun, Đen", 2, 150_000m, 0m), (trousers, "QUAN-01", "Quần kaki", 1, 70_000m, 5_000m)],
            order.Items.Select(item => (item.SkuId, item.SkuCode, item.SkuName, item.Quantity, item.UnitPrice, item.Discount)));

        // reserved tăng đúng số lượng; on_hand không đổi và không có dòng sổ mới.
        Assert.Equal((5, 2, 3), await StockAsync(branch, shirt));
        Assert.Equal((5, 1, 4), await StockAsync(branch, trousers));
        Assert.Equal(2, (await factory.LedgerAsync(_tenantId)).Count);
        // Mỗi dòng đơn có một phần giữ Active hết hạn cùng lúc với đơn.
        var holds = await factory.ReservationsAsync(_tenantId);
        Assert.Equal(
            order.Items.Select(item => (item.Id, item.SkuId, item.Quantity)).Order(),
            holds.Select(hold => (hold.OrderItemId, hold.SkuId, hold.Quantity)).Order());
        Assert.All(holds, hold => Assert.Equal(
            (_tenantId, order.Id, branch, ReservationStatus.Active, order.ReservedUntil, (DateTimeOffset?)null),
            (hold.TenantId, hold.OrderId, hold.BranchId, hold.Status, (DateTimeOffset?)hold.ExpiresAt, hold.ClosedAt)));

        // OrderReserved và StockChanged nằm trong outbox của cùng transaction.
        var reserved = Assert.Single(await factory.OutboxAsync<OrderReserved>(_tenantId));
        Assert.Equal(
            (order.Id, order.OrderNumber, "Admin", (string?)null, branch, 365_000m, order.ReservedUntil),
            (reserved.OrderId, reserved.OrderNumber, reserved.Channel, reserved.ExternalOrderId, reserved.BranchId, reserved.TotalAmount,
                (DateTimeOffset?)reserved.ReservedUntil));
        Assert.Equal(
            new[] { new OrderReservedLine(shirt, 2), new OrderReservedLine(trousers, 1) }.OrderBy(line => line.SkuId),
            reserved.Lines.OrderBy(line => line.SkuId));
        Assert.Contains(new StockChanged(branch, shirt, 5, 2, 3, 100_000m, null, 2), await factory.OutboxAsync<StockChanged>(_tenantId));
        Assert.Contains(new StockChanged(branch, trousers, 5, 1, 4, 100_000m, null, 2), await factory.OutboxAsync<StockChanged>(_tenantId));

        // Đổi giá sau khi tạo đơn không đổi đơn giá trên dòng đơn.
        await factory.QueryAsync(_tenantId, async db =>
        {
            (await db.SkuRefs.SingleAsync(sku => sku.SkuId == shirt)).Apply("AO-01", "Áo thun, Đen", [], 199_000, 199_000, isActive: true, version: 2);
            return await db.SaveChangesAsync();
        });
        var storedOrder = Assert.Single(await factory.OrdersAsync(_tenantId));
        Assert.Equal((order.Id, _tenantId, OrderChannel.Admin, OrderStatus.Reserved, 365_000m),
            (storedOrder.Id, storedOrder.TenantId, storedOrder.Channel, storedOrder.Status, storedOrder.TotalAmount));
        Assert.Equal([70_000m, 150_000m], storedOrder.Items.Select(item => item.UnitPrice).Order());
        Assert.All(storedOrder.Items, item => Assert.Equal((_tenantId, (decimal?)null), (item.TenantId, item.CostPrice)));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("UseCase", "UC-ORD-02 AC-2")]
    public async Task CreateOrder_OneSkuShort_Returns409WithSkuAndAvailableAndSavesNothing()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var shirt = await factory.SeedSkuAsync(_tenantId, "AO-01");
        var trousers = await factory.SeedSkuAsync(_tenantId, "QUAN-01");
        // SKU chưa có dòng số dư ở chi nhánh: coi như tồn khả dụng bằng 0.
        var neverStocked = await factory.SeedSkuAsync(_tenantId, "MU-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(shirt, 5), In(trousers, 1));

        var response = await PostOrderAsync(
            branch, new { skuId = shirt, quantity = 2 }, new { skuId = trousers, quantity = 3 }, new { skuId = neverStocked, quantity = 1 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("insufficient_stock", problem.GetProperty("code").GetString());
        var details = problem.GetProperty("details").EnumerateArray()
            .Select(line => (line.GetProperty("skuId").GetGuid(), line.GetProperty("requested").GetInt32(), line.GetProperty("available").GetInt32()));
        Assert.Equal(new[] { (trousers, 3, 1), (neverStocked, 1, 0) }.Order(), details.Order());
        // Giữ toàn bộ hoặc từ chối toàn bộ: SKU đủ hàng cũng không bị giữ, và không gì được lưu, kể cả outbox.
        Assert.Empty(await factory.OrdersAsync(_tenantId));
        Assert.Empty(await factory.ReservationsAsync(_tenantId));
        Assert.Equal((5, 0, 5), await StockAsync(branch, shirt));
        Assert.Equal((1, 0, 1), await StockAsync(branch, trousers));
        Assert.Null(await factory.BalanceAsync(_tenantId, branch, neverStocked));
        Assert.Empty(await factory.OutboxAsync<OrderReserved>(_tenantId));
        Assert.Equal(2, (await factory.OutboxAsync<StockChanged>(_tenantId)).Count);
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    // Một SKU xuất hiện ở hai dòng đơn: cộng số lượng lại trước khi kiểm (docs/design/flows/reserve-stock.md).
    [Fact]
    [Trait("UseCase", "UC-ORD-02 AC-2")]
    public async Task CreateOrder_SameSkuOnTwoLines_ChecksTheSumAndHoldsEachLine()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var shirt = await factory.SeedSkuAsync(_tenantId, "AO-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(shirt, 3));

        var tooMany = await PostOrderAsync(branch, new { skuId = shirt, quantity = 2 }, new { skuId = shirt, quantity = 2 });
        var fits = await PostOrderAsync(branch, new { skuId = shirt, quantity = 2 }, new { skuId = shirt, quantity = 1 });

        await tooMany.AssertProblemAsync(HttpStatusCode.Conflict, "insufficient_stock");
        Assert.Equal(HttpStatusCode.Created, fits.StatusCode);
        Assert.Equal((3, 3, 0), await StockAsync(branch, shirt));
        Assert.Equal([1, 2], (await factory.ReservationsAsync(_tenantId)).Select(hold => hold.Quantity).Order());
        Assert.Single(await factory.OrdersAsync(_tenantId));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("UseCase", "UC-ORD-01 AC-1")]
    [Trait("UseCase", "UC-ORD-01 AC-7")]
    public async Task SubmitOrder_EnoughStock_SavesAReservedOrderOfTheChannelAndEmitsOrderReserved()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var shirt = await factory.SeedSkuAsync(_tenantId, "AO-01", retailPrice: 150_000);
        await factory.PostLedgerAsync(_tenantId, branch, In(shirt, 5));

        await factory.SubmitOrderAsync(_tenantId, Online("SP-1001", branch, new SubmitOrderLine("AO-01", 2, 140_000, 10_000)));

        var order = Assert.Single(await factory.OrdersAsync(_tenantId));
        Assert.Equal((OrderChannel.Shopee, "SP-1001", OrderStatus.Reserved, branch, (Guid?)null, 270_000m),
            (order.Channel, order.ExternalOrderId, order.Status, order.BranchId, order.CreatedBy, order.TotalAmount));
        Assert.Equal(order.CreatedAt.AddMinutes(30), order.ReservedUntil);
        // Đơn giá của sàn được giữ nguyên, không lấy giá lẻ.
        var item = Assert.Single(order.Items);
        Assert.Equal((shirt, "AO-01", 2, 140_000m, 10_000m), (item.SkuId, item.SkuCode, item.Quantity, item.UnitPrice, item.Discount));
        Assert.Equal((5, 2, 3), await StockAsync(branch, shirt));
        Assert.Equal(ReservationStatus.Active, Assert.Single(await factory.ReservationsAsync(_tenantId)).Status);
        var reserved = Assert.Single(await factory.OutboxAsync<OrderReserved>(_tenantId));
        Assert.Equal((order.Id, "Shopee", "SP-1001"), (reserved.OrderId, reserved.Channel, reserved.ExternalOrderId));
        Assert.Empty(await factory.OutboxAsync<OrderRejected>(_tenantId));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("Scenario", "T04")]
    [Trait("UseCase", "UC-ORD-01 AC-3")]
    public async Task SubmitOrder_SecondSkuShort_RejectsTheWholeOrderAndReservesNothing()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var shirt = await factory.SeedSkuAsync(_tenantId, "AO-01");
        var trousers = await factory.SeedSkuAsync(_tenantId, "QUAN-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(shirt, 5), In(trousers, 1));

        await factory.SubmitOrderAsync(_tenantId, Online(
            "SP-1002", branch, new SubmitOrderLine("AO-01", 1, 150_000, 0), new SubmitOrderLine("QUAN-01", 5, 80_000, 0)));

        Assert.Empty(await factory.OrdersAsync(_tenantId));
        Assert.Empty(await factory.ReservationsAsync(_tenantId));
        Assert.Equal((5, 0, 5), await StockAsync(branch, shirt));
        Assert.Equal((1, 0, 1), await StockAsync(branch, trousers));
        var rejected = Assert.Single(await factory.OutboxAsync<OrderRejected>(_tenantId));
        Assert.Equal(("Shopee", "SP-1002", branch, "InsufficientStock"), (rejected.Channel, rejected.ExternalOrderId, rejected.BranchId, rejected.Reason));
        Assert.Equal(new OrderRejectedDetail("QUAN-01", Requested: 5, Available: 1), Assert.Single(rejected.Details));
        Assert.Empty(await factory.OutboxAsync<OrderReserved>(_tenantId));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("Scenario", "T03")]
    [Trait("UseCase", "UC-ORD-01 AC-4")]
    public async Task SubmitOrder_SameExternalOrderTwice_KeepsOneOrderAndReservesOnce()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var shirt = await factory.SeedSkuAsync(_tenantId, "AO-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(shirt, 5));
        var message = Online("SP-1003", branch, new SubmitOrderLine("AO-01", 2, 150_000, 0));

        await factory.SubmitOrderAsync(_tenantId, message);
        await factory.SubmitOrderAsync(_tenantId, message);
        // Cùng mã đơn ở kênh khác là một đơn khác.
        await factory.SubmitOrderAsync(_tenantId, message with { Channel = "Lazada" });

        Assert.Equal(
            [(OrderChannel.Shopee, "SP-1003"), (OrderChannel.Lazada, "SP-1003")],
            (await factory.OrdersAsync(_tenantId)).Select(order => (order.Channel, order.ExternalOrderId)).Order());
        Assert.Equal((5, 4, 1), await StockAsync(branch, shirt));
        Assert.Equal(2, (await factory.ReservationsAsync(_tenantId)).Count);
        Assert.Equal(2, (await factory.OutboxAsync<OrderReserved>(_tenantId)).Count);
        Assert.Empty(await factory.OutboxAsync<OrderRejected>(_tenantId));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("UseCase", "UC-ORD-01 AC-6")]
    public async Task SubmitOrder_UnknownOrInactiveSkuOrBranch_IsRejectedWithTheReason()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var closedBranch = await factory.SeedBranchAsync(_tenantId, isActive: false);
        var shirt = await factory.SeedSkuAsync(_tenantId, "AO-01");
        await factory.SeedSkuAsync(_tenantId, "AO-02", isActive: false);
        await factory.PostLedgerAsync(_tenantId, branch, In(shirt, 5));
        var line = new SubmitOrderLine("AO-01", 1, 150_000, 0);

        await factory.SubmitOrderAsync(_tenantId, Online("SP-1", branch, line, new SubmitOrderLine("KHONG-CO", 3, 1, 0)));
        await factory.SubmitOrderAsync(_tenantId, Online("SP-2", branch, line, new SubmitOrderLine("AO-02", 2, 1, 0)));
        await factory.SubmitOrderAsync(_tenantId, Online("SP-3", closedBranch, line));
        // Chi nhánh chưa có bản sao ở `core`.
        await factory.SubmitOrderAsync(_tenantId, Online("SP-4", Guid.NewGuid(), line));

        var rejected = (await factory.OutboxAsync<OrderRejected>(_tenantId)).ToDictionary(message => message.ExternalOrderId);
        Assert.Equal(("UnknownSku", new OrderRejectedDetail("KHONG-CO", 3, 0)), (rejected["SP-1"].Reason, Assert.Single(rejected["SP-1"].Details)));
        Assert.Equal(("InactiveSku", new OrderRejectedDetail("AO-02", 2, 0)), (rejected["SP-2"].Reason, Assert.Single(rejected["SP-2"].Details)));
        Assert.Equal(("InactiveBranch", 0), (rejected["SP-3"].Reason, rejected["SP-3"].Details.Count));
        Assert.Equal(("InactiveBranch", 0), (rejected["SP-4"].Reason, rejected["SP-4"].Details.Count));
        Assert.Empty(await factory.OrdersAsync(_tenantId));
        Assert.Equal((5, 0, 5), await StockAsync(branch, shirt));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("UseCase", "UC-ORD-02 AC-4")]
    public async Task CreateOrder_InactiveBranchOrSku_Returns409AndSavesNothing()
    {
        var closedBranch = await factory.SeedBranchAsync(_tenantId, isActive: false);
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        var discontinued = await factory.SeedSkuAsync(_tenantId, "AO-02", isActive: false);

        var atClosedBranch = await PostOrderAsync(closedBranch, new { skuId = sku, quantity = 1 });
        var withDiscontinuedSku = await PostOrderAsync(branch, new { skuId = sku, quantity = 1 }, new { skuId = discontinued, quantity = 1 });

        await atClosedBranch.AssertProblemAsync(HttpStatusCode.Conflict, "inactive_reference");
        await withDiscontinuedSku.AssertProblemAsync(HttpStatusCode.Conflict, "inactive_reference");
        Assert.Empty(await factory.OrdersAsync(_tenantId));
    }

    // SKU hoặc chi nhánh vừa tạo chưa tới `core` do event trễ: 409 để giao diện tự thử lại.
    [Fact]
    [Trait("UseCase", "UC-ORD-02 AC-1")]
    public async Task CreateOrder_BranchOrSkuNotYetInCore_Returns409ReferenceNotReadyAndSavesNothing()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");

        var unknownBranch = await PostOrderAsync(Guid.NewGuid(), new { skuId = sku, quantity = 1 });
        var unknownSku = await PostOrderAsync(branch, new { skuId = sku, quantity = 1 }, new { skuId = Guid.NewGuid(), quantity = 1 });

        await unknownBranch.AssertProblemAsync(HttpStatusCode.Conflict, "reference_not_ready");
        await unknownSku.AssertProblemAsync(HttpStatusCode.Conflict, "reference_not_ready");
        Assert.Empty(await factory.OrdersAsync(_tenantId));
    }

    [Fact]
    [Trait("UseCase", "UC-ORD-02 AC-1")]
    public async Task CreateOrder_InvalidInput_Returns400AndSavesNothing()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01", retailPrice: 150_000);

        HttpResponseMessage[] responses =
        [
            await PostOrderAsync(branch),
            await Staff.PostAsJsonAsync("/orders", new { branchId = branch }),
            await PostOrderAsync(branch, new { skuId = sku, quantity = 0 }),
            await PostOrderAsync(branch, new { skuId = sku, quantity = -1 }),
            await PostOrderAsync(branch, new { skuId = sku, quantity = 1, unitPrice = -1 }),
            await PostOrderAsync(branch, new { skuId = sku, quantity = 1, discount = -1 }),
            // Giảm giá vượt thành tiền của dòng: 2 × 150.000.
            await PostOrderAsync(branch, new { skuId = sku, quantity = 2, discount = 300_001 }),
            await PostOrderAsync(Guid.Empty, new { skuId = sku, quantity = 1 }),
        ];

        foreach (var response in responses)
            await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        Assert.Empty(await factory.OrdersAsync(_tenantId));
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-03 AC-4")]
    public async Task CreateOrder_CashierOrNoToken_IsRefused()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        var body = new { branchId = branch, items = new[] { new { skuId = sku, quantity = 1 } } };

        var asCashier = await factory.CreateClient(Roles.Cashier, _tenantId).PostAsJsonAsync("/orders", body);
        var anonymous = await factory.CreateClient().PostAsJsonAsync("/orders", body);

        await asCashier.AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        await anonymous.AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthenticated");
        Assert.Empty(await factory.OrdersAsync(_tenantId));
    }

    // T14 cho đơn: tenant A không gắn được đơn vào chi nhánh hay SKU của tenant B, và không giữ được hàng của B.
    // Với `core`, bản sao của tenant khác không phân biệt được với bản sao chưa tới, nên lời gọi nhận 409 reference_not_ready thay cho 404.
    [Fact]
    [Trait("Scenario", "T14")]
    public async Task CreateOrder_BranchOrSkuOfAnotherTenant_IsRefusedAndItsStockIsNotReserved()
    {
        var otherTenantId = Guid.NewGuid();
        var branchOfOther = await factory.SeedBranchAsync(otherTenantId);
        var skuOfOther = await factory.SeedSkuAsync(otherTenantId, "B-01");
        await factory.PostLedgerAsync(otherTenantId, branchOfOther, In(skuOfOther, 10));
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "A-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(sku, 10));

        var atBranchOfOther = await PostOrderAsync(branchOfOther, new { skuId = sku, quantity = 1 });
        var withSkuOfOther = await PostOrderAsync(branch, new { skuId = skuOfOther, quantity = 1 });
        // Đơn online của A mang mã SKU chỉ có ở B: bị từ chối như SKU không tồn tại.
        await factory.SubmitOrderAsync(_tenantId, Online("SP-1", branch, new SubmitOrderLine("B-01", 1, 1, 0)));

        await atBranchOfOther.AssertProblemAsync(HttpStatusCode.Conflict, "reference_not_ready");
        await withSkuOfOther.AssertProblemAsync(HttpStatusCode.Conflict, "reference_not_ready");
        Assert.Equal("UnknownSku", Assert.Single(await factory.OutboxAsync<OrderRejected>(_tenantId)).Reason);
        Assert.Empty(await factory.OrdersAsync(_tenantId));
        Assert.Empty(await factory.OrdersAsync(otherTenantId));
        Assert.Empty(await factory.ReservationsAsync(otherTenantId));
        Assert.Equal(0, (await factory.BalanceAsync(otherTenantId, branchOfOther, skuOfOther))!.Reserved);
        Assert.Empty(await factory.OutboxAsync<OrderRejected>(otherTenantId));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
        await factory.AssertInventoryInvariantsAsync(otherTenantId);
    }

    private Task<HttpResponseMessage> PostOrderAsync(Guid branchId, params object[] items) =>
        Staff.PostAsJsonAsync("/orders", new { branchId, items });

    private async Task<(int OnHand, int Reserved, int Available)> StockAsync(Guid branchId, Guid skuId)
    {
        var balance = (await factory.BalanceAsync(_tenantId, branchId, skuId))!;
        return (balance.OnHand, balance.Reserved, balance.Available);
    }
}
