using System.Net;
using System.Net.Http.Json;
using Oism.BuildingBlocks.Auth;
using Oism.Contracts;
using Oism.Core.Application;
using Oism.Core.Application.Inventory;
using Oism.Core.Application.Orders.SearchPosSkus;
using Oism.Core.Domain.Orders;
using static Oism.Core.IntegrationTests.TestKit;

namespace Oism.Core.IntegrationTests.Tenancy;

// T14 cho tồn và sổ: tenant A không đọc được và không đổi được số dư, sổ của tenant B.
public sealed class TenantIsolationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    [Trait("Scenario", "T14")]
    public async Task StockAndLedger_IdsOfAnotherTenant_ShowNothingAndLeaveItsBalanceUnchanged()
    {
        var (tenantA, tenantB, branchOfB) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var skuOfB = await factory.SeedSkuAsync(tenantB, "B-01");
        await factory.PostLedgerAsync(tenantB, branchOfB, In(skuOfB, 10));
        var ownerA = factory.CreateClient(Roles.Owner, tenantA);

        PagedResult<StockDto>[] stock =
        [
            (await ownerA.GetFromJsonAsync<PagedResult<StockDto>>("/stock"))!,
            (await ownerA.GetFromJsonAsync<PagedResult<StockDto>>($"/stock?branchId={branchOfB}&skuId={skuOfB}"))!,
        ];
        PagedResult<LedgerLineDto>[] ledger =
        [
            (await ownerA.GetFromJsonAsync<PagedResult<LedgerLineDto>>("/ledger"))!,
            (await ownerA.GetFromJsonAsync<PagedResult<LedgerLineDto>>($"/ledger?branchId={branchOfB}&skuId={skuOfB}"))!,
        ];
        // Ghi sổ ở tenant A với đúng chi nhánh và SKU của B: tạo số dư riêng của A, không chạm dòng của B.
        await factory.PostLedgerAsync(tenantA, branchOfB, In(skuOfB, 3));

        Assert.All(stock, page => Assert.Equal((0, 0), (page.Total, page.Items.Count)));
        Assert.All(ledger, page => Assert.Equal((0, 0), (page.Total, page.Items.Count)));
        var balanceOfB = (await factory.BalanceAsync(tenantB, branchOfB, skuOfB))!;
        Assert.Equal((tenantB, 10, 1L), (balanceOfB.TenantId, balanceOfB.OnHand, balanceOfB.Version));
        Assert.Equal(10, Assert.Single(await factory.LedgerAsync(tenantB)).Quantity);
        Assert.Equal((tenantA, 3), ((await factory.BalanceAsync(tenantA, branchOfB, skuOfB))!.TenantId, Assert.Single(await factory.LedgerAsync(tenantA)).Quantity));
        await factory.AssertInventoryInvariantsAsync(tenantA);
        await factory.AssertInventoryInvariantsAsync(tenantB);
    }

    // W5-02, T14 cho POS: tìm SKU ở chi nhánh của B không thấy gì; bán bằng chi nhánh hoặc SKU của B nhận 409
    // reference_not_ready (quy tắc 3 ở docs/architecture/multi-tenancy.md), không đơn nào được tạo và tồn của B không đổi.
    [Fact]
    [Trait("Scenario", "T14")]
    public async Task Pos_BranchOrSkuOfAnotherTenant_FindsNothingAndCheckoutIsRefused()
    {
        var (tenantA, tenantB) = (Guid.NewGuid(), Guid.NewGuid());
        var branchOfB = await factory.SeedBranchAsync(tenantB);
        var skuOfB = await factory.SeedSkuAsync(tenantB, "B-01", barcodes: "8934567890128");
        await factory.PostLedgerAsync(tenantB, branchOfB, In(skuOfB, 10));
        var branchOfA = await factory.SeedBranchAsync(tenantA);
        var skuOfA = await factory.SeedSkuAsync(tenantA, "A-01");
        await factory.PostLedgerAsync(tenantA, branchOfA, In(skuOfA, 10));
        var ownerA = factory.CreateClient(Roles.Owner, tenantA);

        var search = await ownerA.GetFromJsonAsync<List<PosSkuDto>>($"/pos/skus?branchId={branchOfB}&query=B-01");
        var byBarcode = await ownerA.GetFromJsonAsync<List<PosSkuDto>>($"/pos/skus?branchId={branchOfA}&query=8934567890128");
        HttpResponseMessage[] checkouts =
        [
            await CheckoutAsync(ownerA, branchOfB, skuOfB),
            await CheckoutAsync(ownerA, branchOfB, skuOfA),
            await CheckoutAsync(ownerA, branchOfA, skuOfB),
        ];

        Assert.Empty(search!);
        Assert.Empty(byBarcode!);
        foreach (var response in checkouts)
            await response.AssertProblemAsync(HttpStatusCode.Conflict, "reference_not_ready");
        Assert.Empty(await factory.OrdersAsync(tenantA));
        Assert.Empty(await factory.OrdersAsync(tenantB));
        Assert.Equal((10, 0, 10), await factory.StockAsync(tenantB, branchOfB, skuOfB));
        Assert.Equal((10, 0, 10), await factory.StockAsync(tenantA, branchOfA, skuOfA));
        await factory.AssertInventoryInvariantsAsync(tenantA);
        await factory.AssertInventoryInvariantsAsync(tenantB);
    }

    // W5-02, phía event: SubmitOrder mang tenant của shop. Hai tenant gửi đơn sàn trùng mã đơn và trùng mã SKU: mỗi bên
    // giữ hàng từ kho của chính mình, chỉ mục unique theo tenant không coi đơn của bên kia là đơn trùng. Đơn trỏ tới mã SKU
    // chỉ có ở tenant khác bị từ chối UnknownSku và tồn của tenant kia không bị giữ.
    [Fact]
    [Trait("Scenario", "T14")]
    [Trait("UseCase", "UC-ORD-01 AC-4")]
    public async Task SubmitOrder_FromTwoTenantsWithTheSameCodes_EachReservesFromItsOwnStockOnly()
    {
        var (tenantA, tenantB) = (Guid.NewGuid(), Guid.NewGuid());
        var branchOfA = await factory.SeedBranchAsync(tenantA);
        var branchOfB = await factory.SeedBranchAsync(tenantB);
        var skuOfA = await factory.SeedSkuAsync(tenantA, "AO-01");
        var skuOfB = await factory.SeedSkuAsync(tenantB, "AO-01");
        var onlyInB = await factory.SeedSkuAsync(tenantB, "CHI-B");
        await factory.PostLedgerAsync(tenantA, branchOfA, In(skuOfA, 1));
        await factory.PostLedgerAsync(tenantB, branchOfB, In(skuOfB, 1), In(onlyInB, 5));

        await factory.SubmitOrderAsync(tenantA, Online("SHOPEE-1", branchOfA, new SubmitOrderLine("AO-01", 1, 150_000, 0)));
        await factory.SubmitOrderAsync(tenantB, Online("SHOPEE-1", branchOfB, new SubmitOrderLine("AO-01", 1, 150_000, 0)));
        await factory.SubmitOrderAsync(tenantA, Online("SHOPEE-2", branchOfA, new SubmitOrderLine("CHI-B", 1, 150_000, 0)));

        Assert.Equal(("SHOPEE-1", OrderStatus.Reserved), OrderOf(Assert.Single(await factory.OrdersAsync(tenantA))));
        Assert.Equal(("SHOPEE-1", OrderStatus.Reserved), OrderOf(Assert.Single(await factory.OrdersAsync(tenantB))));
        Assert.Equal((1, 1, 0), await factory.StockAsync(tenantA, branchOfA, skuOfA));
        Assert.Equal((1, 1, 0), await factory.StockAsync(tenantB, branchOfB, skuOfB));
        Assert.Equal((5, 0, 5), await factory.StockAsync(tenantB, branchOfB, onlyInB));
        var rejected = Assert.Single(await factory.OutboxAsync<OrderRejected>(tenantA));
        Assert.Equal(("SHOPEE-2", "UnknownSku"), (rejected.ExternalOrderId, rejected.Reason));
        Assert.Empty(await factory.OutboxAsync<OrderRejected>(tenantB));
        await factory.AssertInventoryInvariantsAsync(tenantA);
        await factory.AssertInventoryInvariantsAsync(tenantB);
    }

    private static (string?, OrderStatus) OrderOf(Order order) => (order.ExternalOrderId, order.Status);

    private static Task<HttpResponseMessage> CheckoutAsync(HttpClient client, Guid branchId, Guid skuId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/pos/checkout")
        {
            Content = JsonContent.Create(new
            {
                branchId,
                items = new[] { new { skuId, quantity = 1 } },
                payment = new { method = "Cash", amount = 1_000_000 },
            }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return client.SendAsync(request);
    }
}
