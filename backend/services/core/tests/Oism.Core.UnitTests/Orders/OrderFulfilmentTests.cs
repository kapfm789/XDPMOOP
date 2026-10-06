using Oism.Core.Domain;
using Oism.Core.Domain.Inventory;
using Oism.Core.Domain.Orders;

namespace Oism.Core.UnitTests.Orders;

// Quy tắc của bước xuất hàng và thanh toán: giá vốn ghi một lần, phần giữ đóng một lần, thanh toán đủ tiền.
public sealed class OrderFulfilmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 3, 15, 27, TimeSpan.Zero);

    [Fact]
    [Trait("UseCase", "UC-ORD-03 AC-2")]
    [Trait("UseCase", "UC-ORD-03 AC-6")]
    public void SnapshotCosts_OnEveryLine_WritesTheCostOnceAndRefusesASecondWrite()
    {
        var order = Order.Create(Guid.NewGuid(), OrderChannel.Admin, null, null, null, Guid.NewGuid(), Now);
        var shirt = order.AddItem(Guid.NewGuid(), "AO-01", "Áo", 2, 150_000, 0);
        var trousers = order.AddItem(Guid.NewGuid(), "QUAN-01", "Quần", 1, 80_000, 0);
        var costs = new Dictionary<Guid, decimal> { [shirt.Id] = 110_000, [trousers.Id] = 40_000 };

        order.SnapshotCosts(costs);

        Assert.Equal(((decimal?)110_000m, (decimal?)40_000m), (shirt.CostPrice, trousers.CostPrice));
        // Không có đường cập nhật lại: giá vốn của đơn cũ không đổi khi giá vốn bình quân đổi.
        Assert.Throws<InvalidOperationException>(() => order.SnapshotCosts(new Dictionary<Guid, decimal> { [shirt.Id] = 120_000, [trousers.Id] = 50_000 }));
        Assert.Equal(((decimal?)110_000m, (decimal?)40_000m), (shirt.CostPrice, trousers.CostPrice));
    }

    [Fact]
    [Trait("UseCase", "UC-POS-02 AC-1")]
    public void Pay_AtLeastTheTotal_RecordsMethodAmountAndCashierOnce()
    {
        var cashierId = Guid.NewGuid();
        var order = Order.Create(Guid.NewGuid(), OrderChannel.POS, null, Guid.NewGuid().ToString(), null, cashierId, Now);
        order.AddItem(Guid.NewGuid(), "AO-01", "Áo", 2, 150_000, 10_000);

        Assert.Throws<ArgumentOutOfRangeException>(() => order.Pay(PaymentMethod.Cash, 289_999, cashierId, Now));
        Assert.Null(order.Payment);
        order.Pay(PaymentMethod.QR, 290_000, cashierId, Now);

        Assert.Equal(
            (order.Id, PaymentMethod.QR, 290_000m, cashierId, Now),
            (order.Payment!.OrderId, order.Payment.Method, order.Payment.Amount, order.Payment.ConfirmedBy, order.Payment.ConfirmedAt));
        Assert.Throws<InvalidOperationException>(() => order.Pay(PaymentMethod.Cash, 290_000, cashierId, Now));
    }

    // docs/design/state-machines.md mục "Phần giữ hàng": Active sang Consumed hoặc Released, rồi dừng.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("UseCase", "UC-ORD-04 AC-2")]
    public void Reservation_ConsumeOrRelease_ClosesTheHoldOnce(bool consume)
    {
        var order = Order.Create(Guid.NewGuid(), OrderChannel.Admin, null, null, null, null, Now);
        var hold = Reservation.Hold(order.BranchId, order.AddItem(Guid.NewGuid(), "AO-01", "Áo", 2, 1, 0), Now.AddMinutes(30), Now);

        if (consume)
            hold.Consume(Now.AddMinutes(1));
        else
            hold.Release(Now.AddMinutes(1));

        Assert.Equal(
            (consume ? ReservationStatus.Consumed : ReservationStatus.Released, (DateTimeOffset?)Now.AddMinutes(1)),
            (hold.Status, hold.ClosedAt));
        Assert.Throws<InvalidStateTransitionException>(() => hold.Release(Now.AddMinutes(2)));
        Assert.Throws<InvalidStateTransitionException>(() => hold.Consume(Now.AddMinutes(2)));
        Assert.Equal((DateTimeOffset?)Now.AddMinutes(1), hold.ClosedAt);
    }

    [Fact]
    [Trait("UseCase", "UC-ORD-04 AC-1")]
    [Trait("UseCase", "UC-ORD-04 AC-4")]
    public void BalanceRelease_ReducesReservedOnlyAndNeverBelowZero()
    {
        var balance = new InventoryBalance(Guid.NewGuid(), Guid.NewGuid());
        balance.Post(LedgerType.IN, LedgerReason.Purchase, 5, 100_000, LedgerReference.PurchaseReceipt, Guid.NewGuid(), null, Now);
        balance.Reserve(3, Now);

        balance.Release(2, Now.AddMinutes(1));

        Assert.Equal((5, 1, 4, 3L, Now.AddMinutes(1)), (balance.OnHand, balance.Reserved, balance.Available, balance.Version, balance.UpdatedAt));
        Assert.Throws<ArgumentOutOfRangeException>(() => balance.Release(2, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => balance.Release(0, Now));
        Assert.Equal((5, 1, 3L), (balance.OnHand, balance.Reserved, balance.Version));
    }
}
