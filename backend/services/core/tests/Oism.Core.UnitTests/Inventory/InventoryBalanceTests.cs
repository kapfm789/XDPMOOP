using Oism.Core.Domain.Inventory;

namespace Oism.Core.UnitTests.Inventory;

// InventoryBalance.Post là cửa duy nhất đổi OnHand (docs/architecture/transactions-and-concurrency.md).
public sealed class InventoryBalanceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 3, 15, 27, TimeSpan.Zero);
    private static readonly Guid Receipt = Guid.NewGuid();

    [Fact]
    [Trait("UseCase", "UC-INV-01 AC-3")]
    public void Post_InThenOut_ChangesOnHandAndReturnsOneLineCarryingTheBalanceAfter()
    {
        var (branchId, skuId, userId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var balance = new InventoryBalance(branchId, skuId);

        var received = balance.Post(LedgerType.IN, LedgerReason.Purchase, 10, 100_000, LedgerReference.PurchaseReceipt, Receipt, userId, Now);
        var adjusted = balance.Post(LedgerType.OUT, LedgerReason.StocktakeAdjust, 3, 100_000, LedgerReference.Stocktake, Receipt, createdBy: null, Now.AddMinutes(1));

        Assert.Equal((7, 0, 7, 2L, Now.AddMinutes(1)), (balance.OnHand, balance.Reserved, balance.Available, balance.Version, balance.UpdatedAt));
        Assert.Equal(
            (branchId, skuId, LedgerType.IN, LedgerReason.Purchase, 10, 10, 100_000m, LedgerReference.PurchaseReceipt, Receipt, (Guid?)userId, Now),
            (received.BranchId, received.SkuId, received.Type, received.Reason, received.Quantity, received.BalanceAfter, received.UnitCost,
                received.ReferenceType, received.ReferenceId, received.CreatedBy, received.CreatedAt));
        Assert.Equal((LedgerType.OUT, 3, 7, (Guid?)null), (adjusted.Type, adjusted.Quantity, adjusted.BalanceAfter, adjusted.CreatedBy));
        Assert.NotEqual(received.Id, adjusted.Id);
    }

    // Sửa sai bằng dòng Reversal trỏ về dòng gốc (NFR-SEC-03); dòng gốc không đổi.
    [Fact]
    [Trait("UseCase", "UC-INV-01 AC-4")]
    public void Post_Reversal_AddsALinePointingAtTheOriginalAndLeavesItUntouched()
    {
        var balance = new InventoryBalance(Guid.NewGuid(), Guid.NewGuid());
        var original = balance.Post(LedgerType.IN, LedgerReason.Purchase, 10, 100_000, LedgerReference.PurchaseReceipt, Receipt, null, Now);

        var reversal = balance.Post(
            LedgerType.OUT, LedgerReason.Reversal, 10, 100_000, LedgerReference.PurchaseReceipt, Receipt, null, Now, reversalOfId: original.Id);

        Assert.Equal((original.Id, LedgerReason.Reversal, 0), (reversal.ReversalOfId, reversal.Reason, reversal.BalanceAfter));
        Assert.Equal((10, 10, (Guid?)null), (original.Quantity, original.BalanceAfter, original.ReversalOfId));
        Assert.Equal(0, balance.OnHand);
    }

    [Fact]
    [Trait("UseCase", "UC-INV-01 AC-3")]
    public void Post_OutBeyondAvailable_ThrowsInsufficientStockAndChangesNothing()
    {
        var skuId = Guid.NewGuid();
        var balance = new InventoryBalance(Guid.NewGuid(), skuId);
        balance.Post(LedgerType.IN, LedgerReason.Purchase, 2, 100_000, LedgerReference.PurchaseReceipt, Receipt, null, Now);

        var failure = Assert.Throws<InsufficientStockException>(() =>
            balance.Post(LedgerType.OUT, LedgerReason.TransferOut, 3, 100_000, LedgerReference.StockTransfer, Receipt, null, Now));

        Assert.Equal("insufficient_stock", failure.Code);
        Assert.Equal(new StockShortage(skuId, Requested: 3, Available: 2), Assert.Single((IReadOnlyList<StockShortage>)failure.Details!));
        Assert.Equal((2, 1L), (balance.OnHand, balance.Version));
    }

    // FR-COST-01: nhập mua và nhận chuyển kho tính lại giá vốn bình quân; mọi bút toán khác giữ nguyên giá vốn.
    [Fact]
    [Trait("Scenario", "T09")]
    [Trait("UseCase", "UC-INV-02 AC-3")]
    [Trait("UseCase", "UC-INV-02 AC-4")]
    public void Post_PurchaseOrTransferIn_RecalculatesAverageCostAndOtherEntriesLeaveIt()
    {
        var balance = new InventoryBalance(Guid.NewGuid(), Guid.NewGuid());

        // SKU chưa từng có ở chi nhánh: giá vốn bằng đúng đơn giá nhập.
        balance.Post(LedgerType.IN, LedgerReason.Purchase, 10, 100_000, LedgerReference.PurchaseReceipt, Receipt, null, Now);
        Assert.Equal(100_000m, balance.AvgCost);

        var second = balance.Post(LedgerType.IN, LedgerReason.Purchase, 5, 130_000, LedgerReference.PurchaseReceipt, Receipt, null, Now);
        Assert.Equal((15, 110_000m), (balance.OnHand, balance.AvgCost));
        // Dòng sổ mang đơn giá nhập, không mang giá vốn bình quân.
        Assert.Equal(130_000m, second.UnitCost);

        balance.Post(LedgerType.OUT, LedgerReason.Sale, 5, 110_000, LedgerReference.Order, Receipt, null, Now);
        balance.Post(LedgerType.IN, LedgerReason.StocktakeAdjust, 5, 999_000, LedgerReference.Stocktake, Receipt, null, Now);
        Assert.Equal((15, 110_000m), (balance.OnHand, balance.AvgCost));

        balance.Post(LedgerType.IN, LedgerReason.TransferIn, 15, 120_000, LedgerReference.StockTransfer, Receipt, null, Now);
        Assert.Equal((30, 115_000m), (balance.OnHand, balance.AvgCost));
    }

    // FR-RSE-01: giữ hàng tăng reserved, không đổi on_hand; tồn khả dụng là on_hand trừ reserved.
    [Fact]
    [Trait("UseCase", "UC-ORD-01 AC-2")]
    public void Reserve_WithinAvailable_RaisesReservedAndLeavesOnHand()
    {
        var balance = new InventoryBalance(Guid.NewGuid(), Guid.NewGuid());
        balance.Post(LedgerType.IN, LedgerReason.Purchase, 5, 100_000, LedgerReference.PurchaseReceipt, Receipt, null, Now);

        balance.Reserve(3, Now.AddMinutes(1));

        Assert.Equal((5, 3, 2, 2L, Now.AddMinutes(1)), (balance.OnHand, balance.Reserved, balance.Available, balance.Version, balance.UpdatedAt));
    }

    [Fact]
    [Trait("UseCase", "UC-ORD-01 AC-5")]
    public void Reserve_BeyondAvailable_ThrowsInsufficientStockAndChangesNothing()
    {
        var skuId = Guid.NewGuid();
        var balance = new InventoryBalance(Guid.NewGuid(), skuId);
        balance.Post(LedgerType.IN, LedgerReason.Purchase, 5, 100_000, LedgerReference.PurchaseReceipt, Receipt, null, Now);
        balance.Reserve(4, Now);

        var failure = Assert.Throws<InsufficientStockException>(() => balance.Reserve(2, Now));
        // Hàng đang được giữ cũng không xuất được.
        Assert.Throws<InsufficientStockException>(() =>
            balance.Post(LedgerType.OUT, LedgerReason.TransferOut, 2, 100_000, LedgerReference.StockTransfer, Receipt, null, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => balance.Reserve(0, Now));

        Assert.Equal(new StockShortage(skuId, Requested: 2, Available: 1), Assert.Single(failure.Shortages));
        Assert.Equal((5, 4, 2L), (balance.OnHand, balance.Reserved, balance.Version));
    }

    [Fact]
    [Trait("UseCase", "UC-INV-05 AC-1")]
    [Trait("UseCase", "UC-INV-05 AC-2")]
    [Trait("UseCase", "UC-INV-05 AC-3")]
    public void SetThreshold_ValueThenNullThenNegative_SetsClearsAndRejects()
    {
        var balance = new InventoryBalance(Guid.NewGuid(), Guid.NewGuid());

        balance.SetThreshold(0, Now);
        balance.SetThreshold(5, Now.AddMinutes(1));
        Assert.Equal((5, 2L, Now.AddMinutes(1)), (balance.ReorderThreshold, balance.Version, balance.UpdatedAt));

        balance.SetThreshold(null, Now.AddMinutes(2));
        Assert.Equal((null, 3L), (balance.ReorderThreshold, balance.Version));

        Assert.Throws<ArgumentOutOfRangeException>(() => balance.SetThreshold(-1, Now));
        // Ngưỡng không đụng tới tồn và giá vốn.
        Assert.Equal((null, 3L, 0, 0, 0m), (balance.ReorderThreshold, balance.Version, balance.OnHand, balance.Reserved, balance.AvgCost));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Post_QuantityNotPositive_Throws(int quantity)
    {
        var balance = new InventoryBalance(Guid.NewGuid(), Guid.NewGuid());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            balance.Post(LedgerType.IN, LedgerReason.Purchase, quantity, 100_000, LedgerReference.PurchaseReceipt, Receipt, null, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            balance.Post(LedgerType.IN, LedgerReason.Purchase, 1, -1, LedgerReference.PurchaseReceipt, Receipt, null, Now));

        Assert.Equal((0, 0L), (balance.OnHand, balance.Version));
    }
}
