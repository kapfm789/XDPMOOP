using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Auth;
using Oism.Core.Application.Orders;
using Oism.Core.Domain.Orders;

namespace Oism.Core.IntegrationTests.Orders;

// W2-04: tạo đơn thủ công theo Canonical Order (UC-ORD-02, phần FR-ORD-01).
public sealed class ReserveStockTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private HttpClient Staff => factory.CreateClient(Roles.Staff, _tenantId);

    [Fact]
    [Trait("UseCase", "UC-ORD-02 AC-1")]
    [Trait("UseCase", "UC-ORD-02 AC-3")]
    [Trait("UseCase", "UC-PROD-04 AC-4")]
    public async Task CreateOrder_ValidLines_SavesAnAdminOrderWithSnapshotsAndRetailPriceByDefault()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var shirt = await factory.SeedSkuAsync(_tenantId, "AO-01", "Áo thun, Đen", retailPrice: 150_000);
        var trousers = await factory.SeedSkuAsync(_tenantId, "QUAN-01", "Quần kaki", retailPrice: 80_000);

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
        // Bước giữ hàng thuộc W2-05: tới lúc đó đơn dừng ở Draft; có W2-05 thì dòng này đổi thành Reserved.
        Assert.Equal(("Admin", "Draft", branch, "Khách gọi điện", 365_000m), (order.Channel, order.Status, order.BranchId, order.Note, order.TotalAmount));
        Assert.StartsWith("DH", order.OrderNumber);
        Assert.NotNull(order.CreatedBy);
        Assert.Null(order.ExternalOrderId);
        Assert.Equal(
            [(shirt, "AO-01", "Áo thun, Đen", 2, 150_000m, 0m), (trousers, "QUAN-01", "Quần kaki", 1, 70_000m, 5_000m)],
            order.Items.Select(item => (item.SkuId, item.SkuCode, item.SkuName, item.Quantity, item.UnitPrice, item.Discount)));

        // Đổi giá sau khi tạo đơn không đổi đơn giá trên dòng đơn.
        await factory.QueryAsync(_tenantId, async db =>
        {
            (await db.SkuRefs.SingleAsync(sku => sku.SkuId == shirt)).Apply("AO-01", "Áo thun, Đen", [], 199_000, 199_000, isActive: true, version: 2);
            return await db.SaveChangesAsync();
        });
        var stored = await StoredOrdersAsync(_tenantId);
        var storedOrder = Assert.Single(stored);
        Assert.Equal((order.Id, _tenantId, OrderChannel.Admin, OrderStatus.Draft, 365_000m),
            (storedOrder.Id, storedOrder.TenantId, storedOrder.Channel, storedOrder.Status, storedOrder.TotalAmount));
        Assert.Equal([70_000m, 150_000m], storedOrder.Items.Select(item => item.UnitPrice).Order());
        Assert.All(storedOrder.Items, item => Assert.Equal((_tenantId, (decimal?)null), (item.TenantId, item.CostPrice)));
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
        Assert.Empty(await StoredOrdersAsync(_tenantId));
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
        Assert.Empty(await StoredOrdersAsync(_tenantId));
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
        Assert.Empty(await StoredOrdersAsync(_tenantId));
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
        Assert.Empty(await StoredOrdersAsync(_tenantId));
    }

    // T14 cho đơn: tenant A không gắn được đơn vào chi nhánh hay SKU của tenant B. Với `core`, bản sao của tenant khác
    // không phân biệt được với bản sao chưa tới, nên lời gọi nhận 409 reference_not_ready thay cho 404.
    [Fact]
    [Trait("Scenario", "T14")]
    public async Task CreateOrder_BranchOrSkuOfAnotherTenant_IsRefusedAndNoCrossTenantOrderExists()
    {
        var otherTenantId = Guid.NewGuid();
        var branchOfOther = await factory.SeedBranchAsync(otherTenantId);
        var skuOfOther = await factory.SeedSkuAsync(otherTenantId, "B-01");
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "A-01");

        var atBranchOfOther = await PostOrderAsync(branchOfOther, new { skuId = sku, quantity = 1 });
        var withSkuOfOther = await PostOrderAsync(branch, new { skuId = skuOfOther, quantity = 1 });

        await atBranchOfOther.AssertProblemAsync(HttpStatusCode.Conflict, "reference_not_ready");
        await withSkuOfOther.AssertProblemAsync(HttpStatusCode.Conflict, "reference_not_ready");
        Assert.Empty(await StoredOrdersAsync(_tenantId));
        Assert.Empty(await StoredOrdersAsync(otherTenantId));
    }

    private Task<HttpResponseMessage> PostOrderAsync(Guid branchId, params object[] items) =>
        Staff.PostAsJsonAsync("/orders", new { branchId, items });

    private Task<List<Order>> StoredOrdersAsync(Guid tenantId) =>
        factory.QueryAsync(tenantId, db => db.Orders.AsNoTracking().Include(order => order.Items).ToListAsync());
}
