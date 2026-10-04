using Oism.Core.Domain;
using Oism.Core.Domain.Orders;
using Oism.Core.Domain.References;

namespace Oism.Core.UnitTests.Orders;

// Máy trạng thái của đơn: docs/design/state-machines.md mục "Đơn hàng" (FR-ORD-02).
public sealed class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 3, 15, 27, TimeSpan.Zero);

    private static readonly HashSet<(OrderStatus From, OrderStatus To)> Documented =
    [
        (OrderStatus.Draft, OrderStatus.Reserved),
        (OrderStatus.Draft, OrderStatus.Cancelled),
        (OrderStatus.Reserved, OrderStatus.Confirmed),
        (OrderStatus.Reserved, OrderStatus.Cancelled),
        (OrderStatus.Confirmed, OrderStatus.Completed),
    ];

    // Điều kiện xong của W2-04: bước chuyển trạng thái sai bị từ chối. Duyệt cả 25 cặp trạng thái:
    // đúng 5 cặp trong bảng được nhận, 20 cặp còn lại ném invalid_state_transition.
    [Fact]
    [Trait("UseCase", "UC-ORD-03 AC-3")]
    [Trait("UseCase", "UC-ORD-04 AC-3")]
    [Trait("UseCase", "UC-ORD-06 AC-2")]
    public void StateMachine_EveryPairOfStatuses_OnlyTheFiveDocumentedTransitionsAreAllowed()
    {
        foreach (var from in Enum.GetValues<OrderStatus>())
        {
            foreach (var to in Enum.GetValues<OrderStatus>())
            {
                Assert.Equal(Documented.Contains((from, to)), OrderStateMachine.CanMove(from, to));
                if (Documented.Contains((from, to)))
                {
                    OrderStateMachine.EnsureCanMove(from, to);
                    continue;
                }

                var failure = Assert.Throws<InvalidStateTransitionException>(() => OrderStateMachine.EnsureCanMove(from, to));
                Assert.Equal("invalid_state_transition", failure.Code);
            }
        }
    }

    [Fact]
    [Trait("UseCase", "UC-ORD-02 AC-1")]
    public void Create_WithItems_IsADraftWithANumberAndTheTotalOfItsLines()
    {
        var (branchId, userId, skuId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var order = Order.Create(branchId, OrderChannel.Admin, externalOrderId: null, idempotencyKey: null, "  Giao buổi sáng ", userId, Now);
        var item = order.AddItem(skuId, "AO-01", "Áo thun, Đen", quantity: 2, unitPrice: 150_000, discount: 10_000);
        order.AddItem(Guid.NewGuid(), "QUAN-01", "Quần kaki", quantity: 1, unitPrice: 80_000, discount: 0);

        Assert.Equal(
            (branchId, OrderChannel.Admin, OrderStatus.Draft, "Giao buổi sáng", (Guid?)userId, Now, 370_000m),
            (order.BranchId, order.Channel, order.Status, order.Note, order.CreatedBy, order.CreatedAt, order.TotalAmount));
        Assert.Matches("^DH[0-9A-F]{12}$", order.OrderNumber);
        Assert.Equal((order.Id, skuId, "AO-01", "Áo thun, Đen", 2, 150_000m, 10_000m, 290_000m, (decimal?)null),
            (item.OrderId, item.SkuId, item.SkuCode, item.SkuName, item.Quantity, item.UnitPrice, item.Discount, item.LineTotal, item.CostPrice));
        Assert.Equal(2, order.Items.Count);
        Assert.NotEqual(order.OrderNumber, Order.Create(branchId, OrderChannel.Admin, null, null, null, userId, Now).OrderNumber);
    }

    [Fact]
    [Trait("UseCase", "UC-ORD-03 AC-1")]
    [Trait("UseCase", "UC-ORD-06 AC-1")]
    public void Lifecycle_ReserveConfirmComplete_MovesThroughTheStatusesAndStampsEachStep()
    {
        var order = Draft();

        order.MarkReserved(Now.AddMinutes(30));
        order.Confirm(Now.AddMinutes(5));
        order.Complete(Now.AddMinutes(10));

        Assert.Equal(
            (OrderStatus.Completed, (DateTimeOffset?)Now.AddMinutes(30), (DateTimeOffset?)Now.AddMinutes(5), (DateTimeOffset?)Now.AddMinutes(10)),
            (order.Status, order.ReservedUntil, order.ConfirmedAt, order.CompletedAt));
        Assert.Null(order.CancelledAt);
    }

    [Theory]
    [InlineData(false, OrderCancelReason.Manual)]
    [InlineData(true, OrderCancelReason.Expired)]
    [Trait("UseCase", "UC-ORD-04 AC-1")]
    public void Cancel_FromDraftOrReserved_IsCancelledWithTheReason(bool reserved, OrderCancelReason reason)
    {
        var order = Draft();
        if (reserved)
            order.MarkReserved(Now.AddMinutes(30));

        order.Cancel(reason, Now.AddMinutes(1));

        Assert.Equal((OrderStatus.Cancelled, (OrderCancelReason?)reason, (DateTimeOffset?)Now.AddMinutes(1)), (order.Status, order.CancelReason, order.CancelledAt));
    }

    // Phần quy tắc của T08: sau Confirmed không hủy được (ADR-0005). Test tích hợp T08 qua API thuộc W3-01.
    [Fact]
    [Trait("UseCase", "UC-ORD-04 AC-3")]
    public void Cancel_AfterConfirmedOrCompleted_ThrowsAndTheOrderIsUnchanged()
    {
        var order = Draft();
        order.MarkReserved(Now.AddMinutes(30));
        order.Confirm(Now);

        Assert.Throws<InvalidStateTransitionException>(() => order.Cancel(OrderCancelReason.Manual, Now));
        order.Complete(Now);
        Assert.Throws<InvalidStateTransitionException>(() => order.Cancel(OrderCancelReason.Manual, Now));

        Assert.Equal((OrderStatus.Completed, (OrderCancelReason?)null, (DateTimeOffset?)null), (order.Status, order.CancelReason, order.CancelledAt));
    }

    [Fact]
    [Trait("UseCase", "UC-ORD-03 AC-3")]
    [Trait("UseCase", "UC-ORD-06 AC-2")]
    public void ConfirmOrComplete_FromTheWrongStatus_ThrowsAndTheOrderIsUnchanged()
    {
        var draft = Draft();
        var cancelled = Draft();
        cancelled.Cancel(OrderCancelReason.Manual, Now);

        Assert.Throws<InvalidStateTransitionException>(() => draft.Confirm(Now));
        Assert.Throws<InvalidStateTransitionException>(() => draft.Complete(Now));
        Assert.Throws<InvalidStateTransitionException>(() => cancelled.MarkReserved(Now));
        Assert.Throws<InvalidStateTransitionException>(() => cancelled.Cancel(OrderCancelReason.Expired, Now));

        Assert.Equal((OrderStatus.Draft, (DateTimeOffset?)null, (DateTimeOffset?)null), (draft.Status, draft.ConfirmedAt, draft.CompletedAt));
        Assert.Equal((OrderStatus.Cancelled, (OrderCancelReason?)OrderCancelReason.Manual), (cancelled.Status, cancelled.CancelReason));
    }

    [Fact]
    public void AddItem_AfterDraftOrWithBadNumbers_Throws()
    {
        var reserved = Draft();
        reserved.MarkReserved(Now.AddMinutes(30));
        var draft = Draft();

        Assert.Throws<InvalidOperationException>(() => reserved.AddItem(Guid.NewGuid(), "AO-02", "Áo", 1, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => draft.AddItem(Guid.NewGuid(), "AO-02", "Áo", 0, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => draft.AddItem(Guid.NewGuid(), "AO-02", "Áo", 1, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => draft.AddItem(Guid.NewGuid(), "AO-02", "Áo", 2, 100, 201));

        Assert.True(OrderItem.IsValidDiscount(quantity: 2, unitPrice: 100, discount: 200));
        Assert.False(OrderItem.IsValidDiscount(quantity: 2, unitPrice: 100, discount: -1));
        Assert.Equal(150_000m, draft.TotalAmount);
    }

    // UC-ORD-02 AC-4: chi nhánh đã tắt và SKU ngừng bán không vào được đơn mới.
    [Fact]
    [Trait("UseCase", "UC-ORD-02 AC-4")]
    public void EnsureActive_InactiveBranchOrSku_ThrowsInactiveReference()
    {
        var (branch, sku) = (new BranchRef(Guid.NewGuid()), new SkuRef(Guid.NewGuid()));
        branch.Apply("CH-01", "Cửa hàng 1", "Store", isActive: true, version: 1);
        sku.Apply("AO-01", "Áo thun", [], 150_000, 120_000, isActive: true, version: 1);
        branch.EnsureActive();
        sku.EnsureActive();

        branch.Apply("CH-01", "Cửa hàng 1", "Store", isActive: false, version: 2);
        sku.Apply("AO-01", "Áo thun", [], 150_000, 120_000, isActive: false, version: 2);

        Assert.Equal("inactive_reference", Assert.Throws<InactiveReferenceException>(branch.EnsureActive).Code);
        Assert.Equal("inactive_reference", Assert.Throws<InactiveReferenceException>(sku.EnsureActive).Code);
    }

    private static Order Draft()
    {
        var order = Order.Create(Guid.NewGuid(), OrderChannel.Admin, externalOrderId: null, idempotencyKey: null, note: null, Guid.NewGuid(), Now);
        order.AddItem(Guid.NewGuid(), "AO-01", "Áo thun, Đen", quantity: 1, unitPrice: 150_000, discount: 0);
        return order;
    }
}
