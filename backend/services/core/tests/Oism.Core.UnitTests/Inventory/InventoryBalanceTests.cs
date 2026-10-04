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
