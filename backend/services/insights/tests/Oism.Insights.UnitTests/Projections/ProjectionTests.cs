using Oism.Insights.Domain.References;
using Oism.Insights.Domain.Sales;
using Oism.Insights.Domain.Stock;

namespace Oism.Insights.UnitTests.Projections;

public sealed class ProjectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 3, 15, 27, TimeSpan.Zero);

    // ADR-0008: doanh thu thuần là quantity × unit_price − discount; giá vốn là quantity × cost_price đã chốt.
    [Fact]
    [Trait("UseCase", "UC-REP-01 AC-2")]
    public void SalesFact_TwoUnitsAt150kWithCost110k_Revenue300kCogs220k()
    {
        var fact = SalesFact.Record(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Shopee", quantity: 2, unitPrice: 150_000, discount: 0,
            costPrice: 110_000, Now);

        Assert.Equal((300_000m, 220_000m), (fact.Revenue, fact.Cogs));
        Assert.Equal(80_000m, fact.Revenue - fact.Cogs);
    }

    // Chiết khấu là số tiền của cả dòng, trừ một lần.
    [Fact]
    [Trait("UseCase", "UC-REP-01 AC-1")]
    public void SalesFact_LineDiscount_SubtractedOnceFromRevenueAndNotFromCogs()
    {
        var fact = SalesFact.Record(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "POS", quantity: 3, unitPrice: 100_000, discount: 25_000,
            costPrice: 60_000, Now);

        Assert.Equal((275_000m, 180_000m, 3), (fact.Revenue, fact.Cogs, fact.Quantity));
    }

    [Fact]
    public void StockSnapshot_NewerVersionOverwrites_OlderOrEqualVersionIsIgnored()
    {
        var snapshot = new StockSnapshot(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(snapshot.Apply(10, 2, 8, 110_000, threshold: 5, version: 3, Now));
        Assert.False(snapshot.Apply(1, 0, 1, 1, threshold: null, version: 2, Now));
        Assert.False(snapshot.Apply(1, 0, 1, 1, threshold: null, version: 3, Now));

        Assert.Equal((10, 2, 8, 110_000m, (int?)5, 3L), (
            snapshot.OnHand, snapshot.Reserved, snapshot.Available, snapshot.AvgCost, snapshot.Threshold, snapshot.Version));
    }

    // UC-REP-03 AC-1..3: cảnh báo khi available <= threshold; không có ngưỡng thì không bao giờ cảnh báo.
    [Theory]
    [Trait("UseCase", "UC-REP-03 AC-1")]
    [Trait("UseCase", "UC-REP-03 AC-2")]
    [Trait("UseCase", "UC-REP-03 AC-3")]
    [InlineData(5, 5, true)]
    [InlineData(4, 5, true)]
    [InlineData(6, 5, false)]
    [InlineData(0, null, false)]
    public void StockSnapshot_IsLow_WhenAvailableAtOrBelowThreshold(int available, int? threshold, bool expected)
    {
        var snapshot = new StockSnapshot(Guid.NewGuid(), Guid.NewGuid());
        snapshot.Apply(available, 0, available, 0, threshold, version: 1, Now);

        Assert.Equal(expected, snapshot.IsLow);
    }

    [Fact]
    public void SkuName_NewerVersionOverwrites_OlderOrEqualVersionIsIgnored()
    {
        var sku = new SkuName(Guid.NewGuid());

        Assert.True(sku.Apply("AO-01", "Áo thun, Đen", version: 2));
        Assert.False(sku.Apply("AO-01", "Bản cũ", version: 1));
        Assert.False(sku.Apply("AO-01", "Cùng version", version: 2));

        Assert.Equal(("AO-01", "Áo thun, Đen", 2L), (sku.SkuCode, sku.Name, sku.Version));
    }
}
