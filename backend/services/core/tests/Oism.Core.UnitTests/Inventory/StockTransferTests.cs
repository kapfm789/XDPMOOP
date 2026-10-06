using Oism.Core.Domain;
using Oism.Core.Domain.Inventory;

namespace Oism.Core.UnitTests.Inventory;

// Máy trạng thái của phiếu chuyển kho: docs/design/state-machines.md mục "Phiếu chuyển kho" (FR-INV-03).
public sealed class StockTransferTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 3, 15, 27, TimeSpan.Zero);

    [Fact]
    [Trait("UseCase", "UC-INV-03 AC-1")]
    [Trait("UseCase", "UC-INV-03 AC-6")]
    public void Lifecycle_ShipThenReceive_StampsEachStepAndASecondReceiveDoesNothing()
    {
        var (from, to, userId, skuId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var transfer = StockTransfer.Create(from, to, [new TransferLine(skuId, 4)], userId);
        var item = Assert.Single(transfer.Items);

        Assert.Equal((from, to, StockTransferStatus.Draft, (Guid?)userId), (transfer.FromBranchId, transfer.ToBranchId, transfer.Status, transfer.CreatedBy));
        Assert.Matches("^CK[0-9A-F]{12}$", transfer.TransferNumber);
        Assert.Equal((transfer.Id, skuId, 4, (decimal?)null), (item.TransferId, item.SkuId, item.Quantity, item.UnitCost));

        transfer.Ship(Now);
        transfer.SnapshotCosts(new Dictionary<Guid, decimal> { [item.Id] = 110_000 });
        var received = transfer.Receive(Now.AddHours(2));
        var receivedAgain = transfer.Receive(Now.AddHours(3));

        Assert.True(received);
        Assert.False(receivedAgain);
        Assert.Equal(
            (StockTransferStatus.Received, (DateTimeOffset?)Now, (DateTimeOffset?)Now.AddHours(2), (decimal?)110_000m),
            (transfer.Status, transfer.ShippedAt, transfer.ReceivedAt, item.UnitCost));
        // Giá vốn nơi gửi ghi một lần lúc xuất.
        Assert.Throws<InvalidOperationException>(() => transfer.SnapshotCosts(new Dictionary<Guid, decimal> { [item.Id] = 1 }));
    }

    [Fact]
    [Trait("UseCase", "UC-INV-03 AC-7")]
    public void Transitions_OutOfOrderOrSameBranch_AreRefused()
    {
        var draft = StockTransfer.Create(Guid.NewGuid(), Guid.NewGuid(), [new TransferLine(Guid.NewGuid(), 1)], null);
        var shipped = StockTransfer.Create(Guid.NewGuid(), Guid.NewGuid(), [new TransferLine(Guid.NewGuid(), 1)], null);
        shipped.Ship(Now);
        var branch = Guid.NewGuid();

        Assert.Throws<InvalidStateTransitionException>(() => draft.Receive(Now));
        Assert.Throws<InvalidStateTransitionException>(() => shipped.Ship(Now.AddMinutes(1)));
        Assert.Throws<ArgumentException>(() => StockTransfer.Create(branch, branch, [new TransferLine(Guid.NewGuid(), 1)], null));
        Assert.Throws<ArgumentOutOfRangeException>(() => StockTransfer.Create(branch, Guid.NewGuid(), [new TransferLine(Guid.NewGuid(), 0)], null));

        Assert.Equal((StockTransferStatus.Draft, (DateTimeOffset?)null), (draft.Status, draft.ReceivedAt));
        Assert.Equal((StockTransferStatus.InTransit, (DateTimeOffset?)Now), (shipped.Status, shipped.ShippedAt));
    }
}
