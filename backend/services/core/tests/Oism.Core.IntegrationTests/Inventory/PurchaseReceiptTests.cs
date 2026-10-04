using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Auth;
using Oism.BuildingBlocks.Messaging;
using Oism.Contracts;
using Oism.Core.Application;
using Oism.Core.Application.Inventory;
using Oism.Core.Domain.Inventory;

namespace Oism.Core.IntegrationTests.Inventory;

// W2-02: phiếu nhập nháp rồi xác nhận, tăng on_hand, tính lại giá vốn bình quân (UC-INV-02, FR-INV-02, FR-COST-01).
public sealed class PurchaseReceiptTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private HttpClient Staff => factory.CreateClient(Roles.Staff, _tenantId);

    [Fact]
    [Trait("Scenario", "T09")]
    [Trait("UseCase", "UC-INV-02 AC-2")]
    [Trait("UseCase", "UC-INV-02 AC-3")]
    [Trait("UseCase", "UC-INV-02 AC-4")]
    public async Task ConfirmPurchaseReceipt_TenAtHundredThenFiveAtOneThirty_AverageCostIs110000()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01", "Áo thun, Đen");
        var supplier = await CreateSupplierAsync(_tenantId);

        var first = await CreateAsync(branch, supplier, new { skuId = sku, quantity = 10, unitCost = 100_000 });
        Assert.Equal(("Draft", branch, supplier), (first.Status, first.BranchId, first.SupplierId));
        Assert.StartsWith("PN", first.ReceiptNumber);
        Assert.Equal((sku, "AO-01", "Áo thun, Đen", 10, 100_000m), Line(Assert.Single(first.Items)));
        // AC-1: phiếu còn Draft thì tồn chưa bị tác động.
        Assert.Null(await factory.BalanceAsync(_tenantId, branch, sku));

        var confirmed = await ConfirmAsync(first.Id);
        Assert.Equal("Confirmed", confirmed.Status);
        Assert.NotNull(confirmed.ConfirmedAt);
        Assert.NotNull(confirmed.ConfirmedBy);
        // AC-4: SKU chưa từng có ở chi nhánh thì giá vốn bằng đúng đơn giá nhập.
        var afterFirst = (await factory.BalanceAsync(_tenantId, branch, sku))!;
        Assert.Equal((10, 100_000m), (afterFirst.OnHand, afterFirst.AvgCost));

        var second = await CreateAsync(branch, supplier, new { skuId = sku, quantity = 5, unitCost = 130_000 });
        await ConfirmAsync(second.Id);

        var balance = (await factory.BalanceAsync(_tenantId, branch, sku))!;
        Assert.Equal((15, 0, 110_000m, 2L), (balance.OnHand, balance.Reserved, balance.AvgCost, balance.Version));
        // AC-2: mỗi dòng phiếu sinh một dòng sổ IN lý do Purchase, mang đơn giá nhập và chứng từ nguồn.
        var ledger = await factory.LedgerAsync(_tenantId);
        Assert.Equal(
            [(LedgerType.IN, LedgerReason.Purchase, 10, 10, 100_000m, first.Id), (LedgerType.IN, LedgerReason.Purchase, 5, 15, 130_000m, second.Id)],
            ledger.Select(line => (line.Type, line.Reason, line.Quantity, line.BalanceAfter, line.UnitCost, line.ReferenceId)));
        Assert.All(ledger, line => Assert.Equal(LedgerReference.PurchaseReceipt, line.ReferenceType));
        // Người xác nhận phiếu là người tạo dòng sổ.
        Assert.Equal(confirmed.ConfirmedBy, ledger[0].CreatedBy);
        Assert.NotNull(ledger[1].CreatedBy);
        // StockChanged nằm trong outbox của cùng transaction, mang số dư sau thay đổi.
        Assert.Equal(
            [new StockChanged(branch, sku, 10, 0, 10, 100_000m, null, 1), new StockChanged(branch, sku, 15, 0, 15, 110_000m, null, 2)],
            (await factory.OutboxAsync<StockChanged>(_tenantId)).OrderBy(message => message.Version));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("Scenario", "T11")]
    [Trait("UseCase", "UC-INV-02 AC-5")]
    public async Task ConfirmPurchaseReceipt_AlreadyConfirmed_ReturnsTheSameReceiptAndChangesNothing()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        var receipt = await CreateAsync(branch, await CreateSupplierAsync(_tenantId), new { skuId = sku, quantity = 10, unitCost = 100_000 });
        var confirmed = await ConfirmAsync(receipt.Id);

        var again = await ConfirmAsync(receipt.Id);

        Assert.Equal((confirmed.Id, "Confirmed", confirmed.ConfirmedAt, confirmed.ConfirmedBy), (again.Id, again.Status, again.ConfirmedAt, again.ConfirmedBy));
        var balance = (await factory.BalanceAsync(_tenantId, branch, sku))!;
        Assert.Equal((10, 100_000m, 1L), (balance.OnHand, balance.AvgCost, balance.Version));
        Assert.Single(await factory.LedgerAsync(_tenantId));
        Assert.Single(await factory.OutboxAsync<StockChanged>(_tenantId));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    // Hai lời gọi xác nhận tới cùng lúc xếp hàng ở khóa dòng phiếu: lời gọi sau thấy phiếu đã Confirmed và không ghi gì.
    [Fact]
    [Trait("Scenario", "T11")]
    [Trait("UseCase", "UC-INV-02 AC-5")]
    public async Task ConfirmPurchaseReceipt_TenRequestsAtOnce_PostsTheLedgerOnce()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        var receipt = await CreateAsync(branch, await CreateSupplierAsync(_tenantId), new { skuId = sku, quantity = 10, unitCost = 100_000 });
        var staff = Staff;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            await gate.Task;
            return await staff.PostAsync($"/purchase-receipts/{receipt.Id}/confirm", null);
        }).ToArray();
        gate.SetResult();
        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(10, (await factory.BalanceAsync(_tenantId, branch, sku))!.OnHand);
        Assert.Single(await factory.LedgerAsync(_tenantId));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("UseCase", "UC-INV-02 AC-1")]
    public async Task UpdatePurchaseReceipt_DraftEditedAndLineRemoved_StockIsUntouchedUntilConfirmAndConfirmedCannotBeEdited()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var shirt = await factory.SeedSkuAsync(_tenantId, "AO-01");
        var trousers = await factory.SeedSkuAsync(_tenantId, "QUAN-01");
        var (supplier, otherSupplier) = (await CreateSupplierAsync(_tenantId), await CreateSupplierAsync(_tenantId));
        var draft = await CreateAsync(
            branch, supplier, new { skuId = shirt, quantity = 10, unitCost = 100_000 }, new { skuId = trousers, quantity = 4, unitCost = 50_000 });

        var response = await Staff.PutAsJsonAsync($"/purchase-receipts/{draft.Id}", new
        {
            supplierId = otherSupplier,
            note = " Sửa lại ",
            items = new[] { new { skuId = shirt, quantity = 3, unitCost = 120_000 } },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var edited = (await response.Content.ReadFromJsonAsync<PurchaseReceiptDto>())!;
        Assert.Equal((draft.Id, draft.ReceiptNumber, "Draft", otherSupplier, "Sửa lại", branch),
            (edited.Id, edited.ReceiptNumber, edited.Status, edited.SupplierId, edited.Note, edited.BranchId));
        Assert.Equal((shirt, "AO-01", "Áo thun", 3, 120_000m), Line(Assert.Single(edited.Items)));
        Assert.Empty(await factory.LedgerAsync(_tenantId));
        Assert.Null(await factory.BalanceAsync(_tenantId, branch, shirt));
        Assert.Empty(await factory.OutboxAsync<StockChanged>(_tenantId));

        await ConfirmAsync(draft.Id);
        var afterConfirm = await Staff.PutAsJsonAsync($"/purchase-receipts/{draft.Id}", new
        {
            supplierId = supplier,
            items = new[] { new { skuId = shirt, quantity = 99, unitCost = 1 } },
        });

        await afterConfirm.AssertProblemAsync(HttpStatusCode.Conflict, "invalid_state_transition");
        var balance = (await factory.BalanceAsync(_tenantId, branch, shirt))!;
        Assert.Equal((3, 120_000m), (balance.OnHand, balance.AvgCost));
        // Dòng đã xóa khỏi phiếu không được nhập.
        Assert.Null(await factory.BalanceAsync(_tenantId, branch, trousers));
        Assert.Equal(3, Assert.Single(Assert.Single(await ListAsync("")).Items).Quantity);
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    // Cùng một SKU ở hai dòng phiếu: xử lý lần lượt, dòng sau dùng kết quả của dòng trước.
    [Fact]
    [Trait("Scenario", "T09")]
    [Trait("UseCase", "UC-INV-02 AC-3")]
    public async Task ConfirmPurchaseReceipt_SameSkuOnTwoLines_AveragesOverBothAndEmitsOneStockChanged()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        var receipt = await CreateAsync(
            branch, await CreateSupplierAsync(_tenantId),
            new { skuId = sku, quantity = 10, unitCost = 100_000 }, new { skuId = sku, quantity = 5, unitCost = 130_000 });

        await ConfirmAsync(receipt.Id);

        var balance = (await factory.BalanceAsync(_tenantId, branch, sku))!;
        Assert.Equal((15, 110_000m, 2L), (balance.OnHand, balance.AvgCost, balance.Version));
        Assert.Equal([10, 5], (await factory.LedgerAsync(_tenantId)).Select(line => line.Quantity).OrderDescending());
        Assert.Equal(new StockChanged(branch, sku, 15, 0, 15, 110_000m, null, 2), Assert.Single(await factory.OutboxAsync<StockChanged>(_tenantId)));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    // Cắm lỗi vào giữa transaction xác nhận: phiếu, số dư, sổ và outbox đều như trước (docs/testing/strategy.md mục "Mẫu test rollback").
    [Fact]
    [Trait("UseCase", "UC-INV-02 AC-7")]
    public async Task ConfirmPurchaseReceipt_FailureInTheMiddle_LeavesReceiptDraftAndWritesNothing()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var shirt = await factory.SeedSkuAsync(_tenantId, "AO-01");
        var trousers = await factory.SeedSkuAsync(_tenantId, "QUAN-01");
        var receipt = await CreateAsync(
            branch, await CreateSupplierAsync(_tenantId),
            new { skuId = shirt, quantity = 10, unitCost = 100_000 }, new { skuId = trousers, quantity = 4, unitCost = 50_000 });
        await using var faulty = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IEventPublisher, FailingOnSecondEventPublisher>()));
        var client = faulty.CreateClient();
        client.DefaultRequestHeaders.Authorization = Staff.DefaultRequestHeaders.Authorization;

        var response = await client.PostAsync($"/purchase-receipts/{receipt.Id}/confirm", null);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("Draft", Assert.Single(await ListAsync("")).Status);
        Assert.Null(await factory.BalanceAsync(_tenantId, branch, shirt));
        Assert.Null(await factory.BalanceAsync(_tenantId, branch, trousers));
        Assert.Empty(await factory.LedgerAsync(_tenantId));
        Assert.Empty(await factory.OutboxAsync<StockChanged>(_tenantId));

        // Hết lỗi thì xác nhận lại được như chưa có gì xảy ra.
        await ConfirmAsync(receipt.Id);
        Assert.Equal(2, (await factory.LedgerAsync(_tenantId)).Count);
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    [Fact]
    [Trait("UseCase", "UC-INV-02 AC-6")]
    public async Task SavePurchaseReceipt_InvalidInput_Returns400AndSavesNothing()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        var supplier = await CreateSupplierAsync(_tenantId);

        HttpResponseMessage[] responses =
        [
            await PostAsync(branch, supplier, new { skuId = sku, quantity = 0, unitCost = 100_000 }),
            await PostAsync(branch, supplier, new { skuId = sku, quantity = -1, unitCost = 100_000 }),
            await PostAsync(branch, supplier, new { skuId = sku, quantity = 1, unitCost = -1 }),
            await PostAsync(branch, supplier),
            await Staff.PostAsJsonAsync("/purchase-receipts", new { branchId = branch, supplierId = supplier }),
            await PostAsync(Guid.Empty, supplier, new { skuId = sku, quantity = 1, unitCost = 1 }),
            await PostAsync(branch, Guid.Empty, new { skuId = sku, quantity = 1, unitCost = 1 }),
            await PostAsync(branch, supplier, new { skuId = Guid.Empty, quantity = 1, unitCost = 1 }),
            await Staff.PostAsJsonAsync("/suppliers", new { name = "  " }),
            await Staff.GetAsync("/purchase-receipts?status=Posted"),
            await Staff.GetAsync("/purchase-receipts?page=0"),
            await Staff.GetAsync("/purchase-receipts?pageSize=101"),
        ];

        foreach (var response in responses)
            await response.AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        Assert.Empty(await ListAsync(""));
    }

    // SKU không có trong sku_refs hoặc chi nhánh đã tắt: từ chối ngay khi tạo phiếu Draft (docs/design/flows/purchase-receipt.md).
    [Fact]
    [Trait("UseCase", "UC-INV-02 AC-1")]
    public async Task CreatePurchaseReceipt_UnknownOrInactiveReference_IsRefusedAndSavesNothing()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var closedBranch = await factory.SeedBranchAsync(_tenantId, isActive: false);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        var supplier = await CreateSupplierAsync(_tenantId);
        var line = new { skuId = sku, quantity = 1, unitCost = 1 };

        await (await PostAsync(Guid.NewGuid(), supplier, line)).AssertProblemAsync(HttpStatusCode.Conflict, "reference_not_ready");
        await (await PostAsync(branch, supplier, line, new { skuId = Guid.NewGuid(), quantity = 1, unitCost = 1 }))
            .AssertProblemAsync(HttpStatusCode.Conflict, "reference_not_ready");
        await (await PostAsync(closedBranch, supplier, line)).AssertProblemAsync(HttpStatusCode.Conflict, "inactive_reference");
        await (await PostAsync(branch, Guid.NewGuid(), line)).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await Staff.PostAsync($"/purchase-receipts/{Guid.NewGuid()}/confirm", null)).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        Assert.Empty(await ListAsync(""));
    }

    [Fact]
    [Trait("UseCase", "UC-INV-02 AC-1")]
    public async Task ListPurchaseReceipts_FilteredByBranchAndStatus_ReturnsNewestFirstWithPaging()
    {
        var (branch, otherBranch) = (await factory.SeedBranchAsync(_tenantId), await factory.SeedBranchAsync(_tenantId));
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        var supplier = await CreateSupplierAsync(_tenantId, "Công ty Vải Việt", "0900000001");
        var line = new { skuId = sku, quantity = 1, unitCost = 1 };
        var oldest = await CreateAsync(branch, supplier, line);
        var confirmed = await ConfirmAsync((await CreateAsync(branch, supplier, line)).Id);
        var elsewhere = await CreateAsync(otherBranch, supplier, line);

        Assert.Equal([elsewhere.Id, confirmed.Id, oldest.Id], (await ListAsync("")).Select(receipt => receipt.Id));
        Assert.Equal([confirmed.Id, oldest.Id], (await ListAsync($"?branchId={branch}")).Select(receipt => receipt.Id));
        Assert.Equal(confirmed.Id, Assert.Single(await ListAsync("?status=Confirmed")).Id);
        Assert.Equal(oldest.Id, Assert.Single(await ListAsync($"?branchId={branch}&status=Draft")).Id);
        var secondPage = (await Staff.GetFromJsonAsync<PagedResult<PurchaseReceiptDto>>("/purchase-receipts?page=2&pageSize=2"))!;
        Assert.Equal((2, 2, 3, oldest.Id), (secondPage.Page, secondPage.PageSize, secondPage.Total, Assert.Single(secondPage.Items).Id));
        Assert.Equal(
            new SupplierDto(supplier, "Công ty Vải Việt", "0900000001", IsActive: true),
            Assert.Single((await Staff.GetFromJsonAsync<List<SupplierDto>>("/suppliers"))!));
    }

    [Fact]
    [Trait("UseCase", "UC-AUTH-03 AC-4")]
    public async Task PurchaseReceipts_CashierOrNoToken_AreRefused()
    {
        var id = Guid.NewGuid();
        var body = new { supplierId = id, items = Array.Empty<object>() };
        Func<HttpClient, Task<HttpResponseMessage>>[] calls =
        [
            client => client.GetAsync("/suppliers"),
            client => client.PostAsJsonAsync("/suppliers", new { name = "NCC" }),
            client => client.GetAsync("/purchase-receipts"),
            client => client.PostAsJsonAsync("/purchase-receipts", body),
            client => client.PutAsJsonAsync($"/purchase-receipts/{id}", body),
            client => client.PostAsync($"/purchase-receipts/{id}/confirm", null),
        ];

        foreach (var call in calls)
        {
            await (await call(factory.CreateClient(Roles.Cashier, _tenantId))).AssertProblemAsync(HttpStatusCode.Forbidden, "forbidden");
            await (await call(factory.CreateClient())).AssertProblemAsync(HttpStatusCode.Unauthorized, "unauthenticated");
        }
    }

    // T14 cho phiếu nhập: tenant A không đọc, sửa, xác nhận được phiếu của tenant B và không gắn được nhà cung cấp của B.
    [Fact]
    [Trait("Scenario", "T14")]
    public async Task PurchaseReceipts_IdsOfAnotherTenant_Return404AndLeaveItsReceiptUntouched()
    {
        var otherTenantId = Guid.NewGuid();
        var other = factory.CreateClient(Roles.Owner, otherTenantId);
        var branchOfOther = await factory.SeedBranchAsync(otherTenantId);
        var skuOfOther = await factory.SeedSkuAsync(otherTenantId, "B-01");
        var supplierOfOther = await CreateSupplierAsync(otherTenantId);
        var receiptOfOther = (await (await other.PostAsJsonAsync("/purchase-receipts", new
        {
            branchId = branchOfOther,
            supplierId = supplierOfOther,
            items = new[] { new { skuId = skuOfOther, quantity = 10, unitCost = 100_000 } },
        })).Content.ReadFromJsonAsync<PurchaseReceiptDto>())!;
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "A-01");
        var line = new { skuId = sku, quantity = 1, unitCost = 1 };

        var confirm = await Staff.PostAsync($"/purchase-receipts/{receiptOfOther.Id}/confirm", null);
        var update = await Staff.PutAsJsonAsync(
            $"/purchase-receipts/{receiptOfOther.Id}", new { supplierId = await CreateSupplierAsync(_tenantId), items = new[] { line } });
        var withSupplierOfOther = await PostAsync(branch, supplierOfOther, line);

        await confirm.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await update.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await withSupplierOfOther.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        Assert.Empty(await ListAsync(""));
        Assert.DoesNotContain((await Staff.GetFromJsonAsync<List<SupplierDto>>("/suppliers"))!, supplier => supplier.Id == supplierOfOther);
        var untouched = Assert.Single((await other.GetFromJsonAsync<PagedResult<PurchaseReceiptDto>>("/purchase-receipts"))!.Items);
        Assert.Equal((receiptOfOther.Id, "Draft", supplierOfOther, 10), (untouched.Id, untouched.Status, untouched.SupplierId, Assert.Single(untouched.Items).Quantity));
        Assert.Null(await factory.BalanceAsync(otherTenantId, branchOfOther, skuOfOther));
        Assert.All(
            await factory.QueryAsync(otherTenantId, db => db.PurchaseReceiptItems.AsNoTracking().ToListAsync()),
            item => Assert.Equal(otherTenantId, item.TenantId));
    }

    private static (Guid, string, string, int, decimal) Line(PurchaseReceiptItemDto item) =>
        (item.SkuId, item.SkuCode, item.SkuName, item.Quantity, item.UnitCost);

    private async Task<Guid> CreateSupplierAsync(Guid tenantId, string name = "Nhà cung cấp", string? phone = null)
    {
        var response = await factory.CreateClient(Roles.Staff, tenantId).PostAsJsonAsync("/suppliers", new { name, phone });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SupplierDto>())!.Id;
    }

    private Task<HttpResponseMessage> PostAsync(Guid branchId, Guid supplierId, params object[] items) =>
        Staff.PostAsJsonAsync("/purchase-receipts", new { branchId, supplierId, items });

    private async Task<PurchaseReceiptDto> CreateAsync(Guid branchId, Guid supplierId, params object[] items)
    {
        var response = await PostAsync(branchId, supplierId, items);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PurchaseReceiptDto>())!;
    }

    private async Task<PurchaseReceiptDto> ConfirmAsync(Guid id)
    {
        var response = await Staff.PostAsync($"/purchase-receipts/{id}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PurchaseReceiptDto>())!;
    }

    private async Task<IReadOnlyList<PurchaseReceiptDto>> ListAsync(string filter) =>
        (await Staff.GetFromJsonAsync<PagedResult<PurchaseReceiptDto>>($"/purchase-receipts{filter}"))!.Items;

    // Lần ghi outbox thứ hai của một request ném lỗi, tức lỗi xảy ra sau khi sổ và số dư của mọi dòng đã được dựng.
    private sealed class FailingOnSecondEventPublisher(Oism.Core.Infrastructure.CoreDbContext db) : IEventPublisher
    {
        private int _calls;

        public void Enqueue<TPayload>(TPayload payload)
            where TPayload : notnull
        {
            if (++_calls == 2)
                throw new InvalidOperationException("Injected failure.");
            db.Add(OutboxMessage.Create(payload, DateTimeOffset.UtcNow));
        }
    }
}
