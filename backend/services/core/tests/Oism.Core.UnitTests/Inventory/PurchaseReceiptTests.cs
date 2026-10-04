using Oism.Core.Domain;
using Oism.Core.Domain.Inventory;

namespace Oism.Core.UnitTests.Inventory;

// Phiếu nhập: Draft sửa được, Confirmed thì không (docs/design/state-machines.md mục "Phiếu nhập").
public sealed class PurchaseReceiptTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 3, 15, 27, TimeSpan.Zero);

    [Fact]
    [Trait("UseCase", "UC-INV-02 AC-1")]
    public void Revise_DraftReceipt_ReplacesSupplierNoteAndLines()
    {
        var (branchId, supplierId, otherSupplierId, shirt, trousers) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var receipt = PurchaseReceipt.Create(
            branchId, supplierId, "  Lô đầu  ", [new PurchaseLine(shirt, 10, 100_000), new PurchaseLine(trousers, 4, 50_000)], Now);

        Assert.Equal((branchId, supplierId, PurchaseReceiptStatus.Draft, "Lô đầu", Now),
            (receipt.BranchId, receipt.SupplierId, receipt.Status, receipt.Note, receipt.CreatedAt));
        Assert.Equal($"PN{receipt.Id.ToString("N")[..12].ToUpperInvariant()}", receipt.ReceiptNumber);
        Assert.Equal(2, receipt.Items.Count);

        receipt.Revise(otherSupplierId, " ", [new PurchaseLine(shirt, 3, 120_000)]);

        var line = Assert.Single(receipt.Items);
        Assert.Equal((receipt.Id, shirt, 3, 120_000m), (line.ReceiptId, line.SkuId, line.Quantity, line.UnitCost));
        Assert.Equal((otherSupplierId, (string?)null), (receipt.SupplierId, receipt.Note));
    }

    [Fact]
    [Trait("Scenario", "T11")]
    [Trait("UseCase", "UC-INV-02 AC-5")]
    public void Confirm_Twice_SecondCallReportsNoChangeAndKeepsTheFirstConfirmation()
    {
        var (first, second) = (Guid.NewGuid(), Guid.NewGuid());
        var receipt = PurchaseReceipt.Create(Guid.NewGuid(), Guid.NewGuid(), null, [new PurchaseLine(Guid.NewGuid(), 10, 100_000)], Now);

        Assert.True(receipt.Confirm(first, Now.AddMinutes(1)));
        Assert.False(receipt.Confirm(second, Now.AddMinutes(2)));

        Assert.Equal((PurchaseReceiptStatus.Confirmed, (Guid?)first, (DateTimeOffset?)Now.AddMinutes(1)),
            (receipt.Status, receipt.ConfirmedBy, receipt.ConfirmedAt));
    }

    [Fact]
    [Trait("UseCase", "UC-INV-02 AC-1")]
    public void Revise_ConfirmedReceipt_ThrowsInvalidStateTransitionAndKeepsItsLines()
    {
        var receipt = PurchaseReceipt.Create(Guid.NewGuid(), Guid.NewGuid(), null, [new PurchaseLine(Guid.NewGuid(), 10, 100_000)], Now);
        receipt.Confirm(null, Now);

        var failure = Assert.Throws<InvalidStateTransitionException>(() =>
            receipt.Revise(Guid.NewGuid(), null, [new PurchaseLine(Guid.NewGuid(), 1, 1)]));

        Assert.Equal("invalid_state_transition", failure.Code);
        Assert.Equal(10, Assert.Single(receipt.Items).Quantity);
    }

    [Theory]
    [Trait("UseCase", "UC-INV-02 AC-6")]
    [InlineData(0, 100)]
    [InlineData(-1, 100)]
    [InlineData(1, -1)]
    public void Create_QuantityNotPositiveOrNegativeUnitCost_Throws(int quantity, decimal unitCost) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PurchaseReceipt.Create(Guid.NewGuid(), Guid.NewGuid(), null, [new PurchaseLine(Guid.NewGuid(), quantity, unitCost)], Now));
}
