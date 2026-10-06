using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Oism.BuildingBlocks.Auth;
using Oism.Contracts;
using Oism.Core.Application;
using Oism.Core.Application.Orders;
using static Oism.Core.IntegrationTests.TestKit;

namespace Oism.Core.IntegrationTests.Orders;

// W3-01: API xem danh sách và chi tiết đơn (UC-ORD-06; FR-ORD-01, FR-ORD-03).
public sealed class ListOrdersTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private HttpClient Staff => factory.CreateClient(Roles.Staff, _tenantId);

    private HttpClient Owner => factory.CreateClient(Roles.Owner, _tenantId);

    [Fact]
    [Trait("UseCase", "UC-ORD-06 AC-4")]
    public async Task ListOrders_FilteredByStatusChannelBranchAndTime_ReturnsOnlyMatchingOrdersNewestFirst()
    {
        var (branch, otherBranch) = (await factory.SeedBranchAsync(_tenantId), await factory.SeedBranchAsync(_tenantId));
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01", retailPrice: 150_000);
        await factory.PostLedgerAsync(_tenantId, branch, In(sku, 20, 110_000));
        await factory.PostLedgerAsync(_tenantId, otherBranch, In(sku, 20, 110_000));
        var staff = Staff;

        var reserved = await staff.CreateOrderAsync(branch, new { skuId = sku, quantity = 1 });
        var confirmed = await staff.CreateOrderAsync(otherBranch, new { skuId = sku, quantity = 2 });
        await staff.PostAsync($"/orders/{confirmed.Id}/confirm", null);
        await factory.SubmitOrderAsync(_tenantId, Online("SP-1", branch, new SubmitOrderLine("AO-01", 1, 140_000, 0)));
        var cancelled = await staff.CreateOrderAsync(branch, new { skuId = sku, quantity = 1 });
        await staff.PostAsync($"/orders/{cancelled.Id}/cancel", null);
        var pos = await PosCheckoutAsync(branch, sku);
        // Đơn của tenant khác không lọt vào danh sách.
        var otherTenantId = Guid.NewGuid();
        var branchOfOther = await factory.SeedBranchAsync(otherTenantId);
        var skuOfOther = await factory.SeedSkuAsync(otherTenantId, "B-01");
        await factory.PostLedgerAsync(otherTenantId, branchOfOther, In(skuOfOther, 5));
        await factory.CreateClient(Roles.Staff, otherTenantId).CreateOrderAsync(branchOfOther, new { skuId = skuOfOther, quantity = 1 });

        var all = await ListAsync(staff, "");
        var byStatus = await ListAsync(staff, "status=Reserved");
        var byChannel = await ListAsync(staff, "channel=Shopee");
        var byBranch = await ListAsync(staff, $"branchId={otherBranch}");
        var combined = await ListAsync(staff, $"status=Completed&channel=POS&branchId={branch}");
        // from và to tính cả hai đầu: đúng thời điểm tạo của một đơn thì ra đúng đơn đó.
        var at = Uri.EscapeDataString(cancelled.CreatedAt.ToString("O"));
        var byTime = await ListAsync(staff, $"from={at}&to={at}");
        var since = await ListAsync(staff, $"from={at}");
        var secondPage = await ListAsync(staff, "page=2&pageSize=2");

        // Mới nhất trước: POS, đơn hủy, Shopee, đơn đã duyệt, đơn đang giữ.
        Assert.Equal((5, 5, 1, 20), (all.Total, all.Items.Count, all.Page, all.PageSize));
        Assert.Equal(
            [("POS", "Completed"), ("Admin", "Cancelled"), ("Shopee", "Reserved"), ("Admin", "Confirmed"), ("Admin", "Reserved")],
            all.Items.Select(order => (order.Channel, order.Status)));
        Assert.Equal([reserved.Id], byStatus.Items.Where(order => order.Channel == "Admin").Select(order => order.Id));
        Assert.Equal(2, byStatus.Total);
        Assert.Equal(("Shopee", "SP-1"), (Assert.Single(byChannel.Items).Channel, byChannel.Items[0].ExternalOrderId));
        Assert.Equal(confirmed.Id, Assert.Single(byBranch.Items).Id);
        Assert.Equal(pos.Id, Assert.Single(combined.Items).Id);
        Assert.Equal(cancelled.Id, Assert.Single(byTime.Items).Id);
        Assert.Equal([pos.Id, cancelled.Id], since.Items.Select(order => order.Id));
        Assert.Equal((5, 2, 2), (secondPage.Total, secondPage.Page, secondPage.Items.Count));
        Assert.Equal(all.Items.Skip(2).Take(2).Select(order => order.Id), secondPage.Items.Select(order => order.Id));
        // Mỗi đơn kèm các dòng của nó; đơn POS kèm thanh toán.
        Assert.All(all.Items, order => Assert.NotEmpty(order.Items));
        Assert.Equal("Cash", all.Items[0].Payment!.Method);
        Assert.All(all.Items.Skip(1), order => Assert.Null(order.Payment));
    }

    [Fact]
    [Trait("UseCase", "UC-ORD-06 AC-4")]
    [Trait("UseCase", "UC-AUTH-03 AC-4")]
    public async Task ListOrders_InvalidFilterOrCashier_IsRefused()
    {
        var staff = Staff;

        HttpResponseMessage[] invalid =
        [
            await staff.GetAsync("/orders?page=0"),
            await staff.GetAsync("/orders?pageSize=0"),
            await staff.GetAsync("/orders?pageSize=101"),
            await staff.GetAsync("/orders?status=Shipped"),
            await staff.GetAsync("/orders?channel=Zalo"),
        ];
        var asCashier = await factory.CreateClient(Roles.Cashier, _tenantId, Guid.NewGuid()).GetAsync("/orders");
        var detailAsCashier = await factory.CreateClient(Roles.Cashier, _tenantId, Guid.NewGuid()).GetAsync($"/orders/{Guid.NewGuid()}");

        foreach (var response in invalid)
            await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await asCashier.AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
        await detailAsCashier.AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    [Trait("UseCase", "UC-ORD-06 AC-3")]
    [Trait("Scenario", "T14")]
    public async Task GetOrder_ById_ReturnsLinesHoldsAndPaymentAndHidesCostFromStaffAndOtherTenants()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01", "Áo thun, Đen", retailPrice: 150_000);
        await factory.PostLedgerAsync(_tenantId, branch, In(sku, 20, 110_000));
        var reserved = await Staff.CreateOrderAsync(branch, new { skuId = sku, quantity = 2 });
        var pos = await PosCheckoutAsync(branch, sku);

        var held = (await Staff.GetFromJsonAsync<OrderDto>($"/orders/{reserved.Id}"))!;
        var soldForOwner = (await Owner.GetFromJsonAsync<OrderDto>($"/orders/{pos.Id}"))!;
        var soldForStaff = await Staff.GetFromJsonAsync<JsonElement>($"/orders/{pos.Id}");
        var unknown = await Staff.GetAsync($"/orders/{Guid.NewGuid()}");
        var asOtherTenant = await factory.CreateClient(Roles.Owner, Guid.NewGuid()).GetAsync($"/orders/{reserved.Id}");

        // Đơn đang giữ hàng: dòng đơn và phần giữ Active hết hạn cùng lúc với đơn; chưa có thanh toán.
        Assert.Equal((reserved.Id, reserved.OrderNumber, "Reserved", 300_000m), (held.Id, held.OrderNumber, held.Status, held.TotalAmount));
        var item = Assert.Single(held.Items);
        Assert.Equal((sku, "AO-01", "Áo thun, Đen", 2, 150_000m), (item.SkuId, item.SkuCode, item.SkuName, item.Quantity, item.UnitPrice));
        var hold = Assert.Single(held.Reservations!);
        Assert.Equal((item.Id, sku, 2, "Active", held.ReservedUntil, (DateTimeOffset?)null),
            (hold.OrderItemId, hold.SkuId, hold.Quantity, hold.Status, (DateTimeOffset?)hold.ExpiresAt, hold.ClosedAt));
        Assert.Null(held.Payment);
        // Đơn POS: phần giữ đã Consumed, có thanh toán; giá vốn chỉ Owner thấy.
        Assert.Equal(("POS", "Completed", "Cash"), (soldForOwner.Channel, soldForOwner.Status, soldForOwner.Payment!.Method));
        Assert.Equal("Consumed", Assert.Single(soldForOwner.Reservations!).Status);
        Assert.NotNull(soldForOwner.Reservations![0].ClosedAt);
        Assert.Equal(110_000m, Assert.Single(soldForOwner.Items).CostPrice);
        Assert.False(soldForStaff.GetProperty("items")[0].TryGetProperty("costPrice", out _));
        await unknown.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await asOtherTenant.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
    }

    private static async Task<PagedResult<OrderDto>> ListAsync(HttpClient client, string query) =>
        (await client.GetFromJsonAsync<PagedResult<OrderDto>>($"/orders?{query}"))!;

    private async Task<OrderDto> PosCheckoutAsync(Guid branchId, Guid skuId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/pos/checkout")
        {
            Content = JsonContent.Create(new
            {
                branchId,
                items = new[] { new { skuId, quantity = 1 } },
                payment = new { method = "Cash", amount = 150_000 },
            }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var response = await Owner.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OrderDto>())!;
    }
}
