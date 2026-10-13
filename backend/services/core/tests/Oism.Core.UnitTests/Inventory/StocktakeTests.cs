using Oism.Core.Domain;
using Oism.Core.Domain.Inventory;

namespace Oism.Core.UnitTests.Inventory;

// Máy trạng thái của phiên kiểm kê: docs/design/state-machines.md mục "Phiên kiểm kê" (FR-INV-04).
public sealed class StocktakeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 3, 15, 27, TimeSpan.Zero);
    private static readonly Guid Branch = Guid.NewGuid();

    [Fact]
    [Trait("UseCase", "UC-INV-04 AC-1")]
    [Trait("UseCase", "UC-INV-04 AC-2")]
    [Trait("UseCase", "UC-INV-04 AC-3")]
    [Trait("UseCase", "UC-INV-04 AC-5")]
    public void Post_CountsAboveBelowAndEqualToTheBooks_SnapshotsBookQuantityAndDifferenceThenTheSessionIsClosed()
    {
        var (over, under, same, userId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var balances = new Dictionary<Guid, InventoryBalance>
        {
            [over] = Balance(over, onHand: 10),
            [under] = Balance(under, onHand: 10),
            [same] = Balance(same, onHand: 5),
        };
        var stocktake = Stocktake.Open(Branch, userId);

        // Nhập lại số đếm là thay cả bộ.
        stocktake.SetCounts([new StocktakeCount(over, 99)]);
        stocktake.SetCounts([new StocktakeCount(over, 12), new StocktakeCount(under, 7), new StocktakeCount(same, 5)]);

        Assert.Equal((Branch, StocktakeStatus.Open, (Guid?)userId, (DateTimeOffset?)null), (stocktake.BranchId, stocktake.Status, stocktake.CreatedBy, stocktake.PostedAt));
        Assert.All(stocktake.Items, item => Assert.Equal((stocktake.Id, (int?)null, (int?)null), (item.StocktakeId, item.SystemQty, item.Difference)));
        Assert.Empty(stocktake.FindBelowReserved(balances));

        stocktake.Post(balances, Now);

        Assert.Equal((StocktakeStatus.Posted, (DateTimeOffset?)Now), (stocktake.Status, stocktake.PostedAt));
        Assert.Equal(
            [(over, 12, 10, 2), (under, 7, 10, -3), (same, 5, 5, 0)],
            stocktake.Items.Select(item => (item.SkuId, item.CountedQty, item.SystemQty!.Value, item.Difference!.Value)));
        // Phiên đã Posted không chốt lại và không nhận số đếm mới.
        Assert.Throws<InvalidStateTransitionException>(() => stocktake.Post(balances, Now.AddMinutes(1)));
        Assert.Throws<InvalidStateTransitionException>(() => stocktake.SetCounts([new StocktakeCount(over, 1)]));
        Assert.Equal((DateTimeOffset?)Now, stocktake.PostedAt);
    }

    [Fact]
    [Trait("Scenario", "T13")]
    [Trait("UseCase", "UC-INV-04 AC-4")]
    public void FindBelowReserved_CountUnderTheHeldQuantity_ReturnsOnlyThatLine()
    {
        var (short1, exact) = (Guid.NewGuid(), Guid.NewGuid());
        var balances = new Dictionary<Guid, InventoryBalance>
        {
            [short1] = Balance(short1, onHand: 10, reserved: 8),
            [exact] = Balance(exact, onHand: 10, reserved: 8),
        };
        var stocktake = Stocktake.Open(Branch, createdBy: null);
        // Đếm đúng bằng lượng đang giữ thì tồn khả dụng về 0, vẫn hợp lệ.
        stocktake.SetCounts([new StocktakeCount(short1, 6), new StocktakeCount(exact, 8)]);

        var below = stocktake.FindBelowReserved(balances);

        Assert.Equal((short1, 6), (Assert.Single(below).SkuId, below[0].CountedQty));
    }

    [Fact]
    public void SetCounts_NegativeCount_IsRefused()
    {
        var stocktake = Stocktake.Open(Branch, createdBy: null);

        Assert.Throws<ArgumentOutOfRangeException>(() => stocktake.SetCounts([new StocktakeCount(Guid.NewGuid(), -1)]));
    }

    private static InventoryBalance Balance(Guid skuId, int onHand, int reserved = 0)
    {
        var balance = new InventoryBalance(Branch, skuId);
        balance.Post(LedgerType.IN, LedgerReason.Purchase, onHand, 100_000, LedgerReference.PurchaseReceipt, Guid.NewGuid(), null, Now);
        if (reserved > 0)
            balance.Reserve(reserved, Now);
        return balance;
    }
}
