using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Auth;
using Oism.Core.Application.Inventory;
using static Oism.Core.IntegrationTests.TestKit;

namespace Oism.Core.IntegrationTests.Inventory;

// W5-01: phần còn hở của module Inventory sau khi đo coverage (docs/testing/coverage.md): chứng từ kho tạo qua API
// mang đúng tenant của token, và chứng từ chỉ được khóa bên trong transaction.
public sealed class InventoryDocumentTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Guid _tenantId = Guid.NewGuid();

    // Interceptor khi ghi gán TenantId từ tenant context cho mọi bản ghi mới (docs/architecture/multi-tenancy.md).
    // Đọc lại bỏ filter tenant để thấy đúng giá trị đã lưu, không phải giá trị filter tự thêm.
    [Fact]
    [Trait("Scenario", "T14")]
    public async Task InventoryDocuments_CreatedThroughApi_StampedWithTheTenantOfTheToken()
    {
        var from = await factory.SeedBranchAsync(_tenantId);
        var to = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        await factory.PostLedgerAsync(_tenantId, from, In(sku, 5));
        var staff = factory.CreateClient(Roles.Staff, _tenantId);

        var supplier = await CreatedAsync<SupplierDto>(staff.PostAsJsonAsync("/suppliers", new { name = "Xưởng may Bình Minh" }));
        var receipt = await CreatedAsync<PurchaseReceiptDto>(staff.PostAsJsonAsync("/purchase-receipts", new
        {
            branchId = from,
            supplierId = supplier.Id,
            items = new[] { new { skuId = sku, quantity = 2, unitCost = 90_000 } },
        }));
        var transfer = await CreatedAsync<TransferDto>(staff.PostAsJsonAsync("/transfers", new
        {
            fromBranchId = from,
            toBranchId = to,
            items = new[] { new { skuId = sku, quantity = 1 } },
        }));
        var stocktake = await CreatedAsync<StocktakeDto>(staff.PostAsJsonAsync("/stocktakes", new { branchId = from }));
        Assert.Equal(
            HttpStatusCode.OK,
            (await staff.PutAsJsonAsync($"/stocktakes/{stocktake.Id}/counts", new { items = new[] { new { skuId = sku, countedQty = 5 } } }))
            .StatusCode);

        var stamps = await factory.QueryAsync(_tenantId, async db => new[]
        {
            (await db.Suppliers.IgnoreQueryFilters().SingleAsync(row => row.Id == supplier.Id)).TenantId,
            (await db.PurchaseReceipts.IgnoreQueryFilters().SingleAsync(row => row.Id == receipt.Id)).TenantId,
            (await db.PurchaseReceiptItems.IgnoreQueryFilters().SingleAsync(row => row.ReceiptId == receipt.Id)).TenantId,
            (await db.StockTransfers.IgnoreQueryFilters().SingleAsync(row => row.Id == transfer.Id)).TenantId,
            (await db.StockTransferItems.IgnoreQueryFilters().SingleAsync(row => row.TransferId == transfer.Id)).TenantId,
            (await db.Stocktakes.IgnoreQueryFilters().SingleAsync(row => row.Id == stocktake.Id)).TenantId,
            (await db.StocktakeItems.IgnoreQueryFilters().SingleAsync(row => row.StocktakeId == stocktake.Id)).TenantId,
        });
        Assert.All(stamps, tenantId => Assert.Equal(_tenantId, tenantId));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    // Khóa dòng chỉ giữ tới hết transaction; khóa ngoài transaction là lỗi lập trình và bị chặn ngay
    // (docs/architecture/transactions-and-concurrency.md).
    [Fact]
    public async Task GetForUpdate_OutsideATransaction_Throws()
    {
        var id = Guid.NewGuid();

        await factory.InTenantAsync(_tenantId, async services =>
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => services.GetRequiredService<IPurchaseReceiptRepository>().GetForUpdateAsync(id, default));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => services.GetRequiredService<IStockTransferRepository>().GetForUpdateAsync(id, default));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => services.GetRequiredService<IStocktakeRepository>().GetForUpdateAsync(id, default));
            return true;
        });
    }

    private static async Task<T> CreatedAsync<T>(Task<HttpResponseMessage> sending)
    {
        var response = await sending;
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
