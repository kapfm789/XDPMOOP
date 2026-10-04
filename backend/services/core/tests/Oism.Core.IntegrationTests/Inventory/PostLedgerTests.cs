using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Oism.Core.Application.Inventory.PostLedger;
using Oism.Core.Domain.Inventory;
using static Oism.Core.IntegrationTests.TestKit;

namespace Oism.Core.IntegrationTests.Inventory;

// W2-01: sổ chỉ thêm mới, số dư và PostLedger (FR-INV-01, NFR-SEC-03).
public sealed class PostLedgerTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _branchId = Guid.NewGuid();

    // Điều kiện xong của W2-01: OnHand bằng tổng ledger.
    [Fact]
    [Trait("UseCase", "UC-INV-01 AC-3")]
    public async Task PostLedger_InsThenOuts_OnHandEqualsInsMinusOutsAndEachChangeLeavesOneLine()
    {
        var sku = Guid.NewGuid();

        // SKU chưa từng có ở chi nhánh: dòng số dư được tạo ở 0 rồi mới khóa.
        await factory.PostLedgerAsync(_tenantId, _branchId, In(sku, 10, unitCost: 100_000));
        // Hai bút toán của cùng một SKU trong một transaction.
        await factory.PostLedgerAsync(_tenantId, _branchId, In(sku, 5, unitCost: 130_000), Out(sku, 3));
        await factory.PostLedgerAsync(_tenantId, _branchId, Out(sku, 2));

        var balance = (await factory.BalanceAsync(_tenantId, _branchId, sku))!;
        Assert.Equal((10, 0, 10, 4L), (balance.OnHand, balance.Reserved, balance.Available, balance.Version));
        var ledger = await factory.LedgerAsync(_tenantId);
        Assert.Equal(
            [(LedgerType.IN, 10, 10), (LedgerType.IN, 5, 15), (LedgerType.OUT, 3, 12), (LedgerType.OUT, 2, 10)],
            ledger.Select(line => (line.Type, line.Quantity, line.BalanceAfter)));
        Assert.Equal([100_000m, 130_000m, 100_000m, 100_000m], ledger.Select(line => line.UnitCost));
        Assert.All(ledger, line =>
        {
            Assert.Equal((_tenantId, _branchId, sku, LedgerReference.PurchaseReceipt), (line.TenantId, line.BranchId, line.SkuId, line.ReferenceType));
            Assert.Null(line.CreatedBy);
        });
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    // Bất biến 8: nghiệp vụ lỗi giữa chừng không để lại gì.
    [Fact]
    [Trait("UseCase", "UC-INV-01 AC-3")]
    public async Task PostLedger_OutBeyondAvailableInTheMiddle_ThrowsAndRollsBackEveryEntry()
    {
        var (stocked, untouched) = (Guid.NewGuid(), Guid.NewGuid());
        await factory.PostLedgerAsync(_tenantId, _branchId, In(stocked, 2));

        var failure = await Assert.ThrowsAsync<InsufficientStockException>(() =>
            factory.PostLedgerAsync(_tenantId, _branchId, In(untouched, 5), Out(stocked, 3)));

        Assert.Equal("insufficient_stock", failure.Code);
        Assert.Equal(new StockShortage(stocked, Requested: 3, Available: 2), Assert.Single((IReadOnlyList<StockShortage>)failure.Details!));
        Assert.Equal(2, (await factory.BalanceAsync(_tenantId, _branchId, stocked))!.OnHand);
        Assert.Null(await factory.BalanceAsync(_tenantId, _branchId, untouched));
        Assert.Single(await factory.LedgerAsync(_tenantId));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    // Điều kiện xong của W2-01: UPDATE/DELETE bị database từ chối.
    [Fact]
    [Trait("Scenario", "T16")]
    [Trait("UseCase", "UC-INV-01 AC-4")]
    public async Task Ledger_DirectUpdateOrDelete_DatabaseRejectsBothAndTheLineIsUnchanged()
    {
        var sku = Guid.NewGuid();
        var line = Assert.Single(await factory.PostLedgerAsync(_tenantId, _branchId, In(sku, 10)));

        var update = await Assert.ThrowsAsync<PostgresException>(() => factory.QueryAsync(_tenantId, db =>
            db.Database.ExecuteSqlAsync($"UPDATE core.inventory_transactions SET quantity = 999 WHERE id = {line.Id}")));
        var delete = await Assert.ThrowsAsync<PostgresException>(() => factory.QueryAsync(_tenantId, db =>
            db.Database.ExecuteSqlAsync($"DELETE FROM core.inventory_transactions WHERE id = {line.Id}")));

        Assert.Contains("append-only", update.MessageText);
        Assert.Contains("append-only", delete.MessageText);
        var stored = Assert.Single(await factory.LedgerAsync(_tenantId));
        Assert.Equal((line.Id, 10, 10), (stored.Id, stored.Quantity, stored.BalanceAfter));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    // Hàng rào cuối ở database: số dư âm hoặc reserved vượt on_hand bị CHECK từ chối, kể cả khi đi vòng qua Domain.
    [Fact]
    [Trait("UseCase", "UC-INV-01 AC-3")]
    public async Task Balance_DirectUpdateBelowZeroOrBelowReserved_DatabaseRejects()
    {
        var sku = Guid.NewGuid();
        await factory.PostLedgerAsync(_tenantId, _branchId, In(sku, 1));

        var negative = await Assert.ThrowsAsync<PostgresException>(() => factory.QueryAsync(_tenantId, db =>
            db.Database.ExecuteSqlAsync($"UPDATE core.inventory_balances SET on_hand = -1 WHERE sku_id = {sku}")));
        var overReserved = await Assert.ThrowsAsync<PostgresException>(() => factory.QueryAsync(_tenantId, db =>
            db.Database.ExecuteSqlAsync($"UPDATE core.inventory_balances SET reserved = 2 WHERE sku_id = {sku}")));

        Assert.Equal(("ck_balance_non_negative", "ck_balance_non_negative"), (negative.ConstraintName, overReserved.ConstraintName));
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    // Khóa dòng chỉ có nghĩa trong transaction của use case.
    [Fact]
    public async Task PostLedger_OutsideATransaction_Throws()
    {
        var command = new PostLedgerCommand(_branchId, LedgerReference.Stocktake, Guid.NewGuid(), [In(Guid.NewGuid(), 1)], CreatedBy: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => factory.InTenantAsync(_tenantId, services =>
            services.GetRequiredService<PostLedgerHandler>().Handle(command, default)));

        Assert.Empty(await factory.LedgerAsync(_tenantId));
    }
}
