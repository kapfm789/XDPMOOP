using Oism.Core.Domain.Inventory;

namespace Oism.Core.UnitTests.Inventory;

// Công thức giá vốn bình quân: docs/decisions/0004-wac-per-branch.md.
public sealed class WeightedAverageCostTests
{
    [Fact]
    [Trait("Scenario", "T09")]
    [Trait("UseCase", "UC-INV-02 AC-3")]
    public void Recalculate_TenAtHundredThenFiveAtOneThirty_AverageCostIs110000() =>
        Assert.Equal(110_000m, WeightedAverageCost.Recalculate(onHand: 10, avgCost: 100_000, quantity: 5, unitCost: 130_000));

    [Fact]
    [Trait("UseCase", "UC-INV-02 AC-4")]
    public void Recalculate_NothingOnHand_AverageCostIsTheUnitCost() =>
        Assert.Equal(130_000m, WeightedAverageCost.Recalculate(onHand: 0, avgCost: 99_000, quantity: 5, unitCost: 130_000));

    // numeric(18,4), làm tròn nửa lên: 0,00025 thành 0,0003 chứ không thành 0,0002 như làm tròn về số chẵn.
    [Theory]
    [InlineData(1, 0.0002, 1, 0.0003, 0.0003)]
    [InlineData(2, 100, 1, 200, 133.3333)]
    [InlineData(1, 100, 2, 200, 166.6667)]
    public void Recalculate_ResultWithMoreThanFourDecimals_RoundsHalfUpToFour(
        int onHand, decimal avgCost, int quantity, decimal unitCost, decimal expected) =>
        Assert.Equal(expected, WeightedAverageCost.Recalculate(onHand, avgCost, quantity, unitCost));
}
