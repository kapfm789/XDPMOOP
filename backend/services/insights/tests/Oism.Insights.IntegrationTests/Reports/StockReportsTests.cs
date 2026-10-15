using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Oism.Contracts;
using Oism.Insights.Application.Reports.GetInventoryValue;
using Oism.Insights.Application.Reports.GetSlowMovers;
using Oism.Insights.Application.Reports.GetTopSellers;
using static Oism.Insights.IntegrationTests.TestKit;

namespace Oism.Insights.IntegrationTests.Reports;

// W4-03, UC-REP-02: GET /reports/inventory-value, /reports/top-sellers, /reports/slow-movers.
public sealed class StockReportsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _branchA = Guid.NewGuid();
    private readonly Guid _branchB = Guid.NewGuid();
    private readonly Guid _skuX = Guid.NewGuid();
    private readonly Guid _skuY = Guid.NewGuid();
    private readonly Guid _skuZ = Guid.NewGuid();

    // Mỗi dòng là on_hand × avg_cost theo chi nhánh và SKU, kèm tổng; dòng hết tồn không có mặt.
    [Fact]
    [Trait("UseCase", "UC-REP-02 AC-1")]
    public async Task InventoryValue_Owner_OnHandTimesAvgCostPerBranchAndSkuWithTotal()
    {
        await factory.UpsertSkuNameAsync(_tenantId, _skuX, "AO-01", "Áo thun, Đen");
        await StockAsync(_branchA, _skuX, onHand: 10, avgCost: 110_000);
        await StockAsync(_branchB, _skuX, onHand: 2, avgCost: 120_000);
        await StockAsync(_branchA, _skuY, onHand: 3, avgCost: 500_000);
        await StockAsync(_branchB, _skuY, onHand: 0, avgCost: 500_000);

        var report = await GetAsync<InventoryValueReport>("Owner", "/reports/inventory-value");

        Assert.Equal(
            [
                (_branchA, _skuY, (string?)null, 3, (decimal?)500_000m, (decimal?)1_500_000m),
                (_branchA, _skuX, "AO-01", 10, 110_000m, 1_100_000m),
                (_branchB, _skuX, "AO-01", 2, 120_000m, 240_000m),
            ],
            report.Items.Select(row => (row.BranchId, row.SkuId, row.SkuCode, row.OnHand, row.AvgCost, row.Value)));
        Assert.Equal((3, 2_840_000m), (report.Total, report.TotalValue));

        var branchA = await GetAsync<InventoryValueReport>("Owner", $"/reports/inventory-value?branchId={_branchA}&pageSize=1");
        Assert.Equal((2, 2_600_000m), (branchA.Total, branchA.TotalValue));
        Assert.Equal(_skuY, Assert.Single(branchA.Items).SkuId);
    }

    // D-15: Staff xem được tồn nhưng không thấy giá vốn hay giá trị.
    [Fact]
    [Trait("UseCase", "UC-REP-02 AC-1")]
    public async Task InventoryValue_Staff_NoAvgCostNoValue()
    {
        await StockAsync(_branchA, _skuX, onHand: 10, avgCost: 110_000);

        var response = await factory.CreateClient("Staff", _tenantId).GetAsync("/reports/inventory-value");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, json.GetProperty("totalValue").ValueKind);
        var row = Assert.Single(json.GetProperty("items").EnumerateArray());
        Assert.Equal(10, row.GetProperty("onHand").GetInt32());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("avgCost").ValueKind);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("value").ValueKind);
    }

    // Bán chạy: SKU xếp giảm dần theo số lượng bán trong 30 ngày gần nhất; bán trước cửa sổ không tính.
    [Fact]
    [Trait("UseCase", "UC-REP-02 AC-2")]
    public async Task TopSellers_Last30Days_SortedByQuantityDescending()
    {
        await factory.UpsertSkuNameAsync(_tenantId, _skuY, "QUAN-01", "Quần jean");
        await SellAsync(_branchA, DaysAgo(1), Line(_skuX, quantity: 2));
        await SellAsync(_branchB, DaysAgo(3), Line(_skuX, quantity: 1), Line(_skuY, quantity: 5));
        await SellAsync(_branchA, DaysAgo(10), Line(_skuZ, quantity: 4));
        // Ngoài cửa sổ 30 ngày.
        await SellAsync(_branchA, DaysAgo(40), Line(_skuX, quantity: 100));

        var top = await GetAsync<List<TopSellerRow>>("Staff", "/reports/top-sellers");

        Assert.Equal(
            [(_skuY, "QUAN-01", 5), (_skuZ, (string?)null, 4), (_skuX, null, 3)],
            top.Select(row => (row.SkuId, row.SkuCode, row.Quantity)));

        var branchA = await GetAsync<List<TopSellerRow>>("Owner", $"/reports/top-sellers?branchId={_branchA}&limit=1");
        Assert.Equal((_skuZ, 4), (Assert.Single(branchA).SkuId, branchA[0].Quantity));
        var lastWeek = await GetAsync<List<TopSellerRow>>("Owner", "/reports/top-sellers?days=7");
        Assert.Equal([_skuY, _skuX], lastWeek.Select(row => row.SkuId));
    }

    // Bán chậm: chỉ SKU còn tồn, xếp tăng dần theo số lượng bán; SKU không bán được gì đứng đầu.
    [Fact]
    [Trait("UseCase", "UC-REP-02 AC-3")]
    public async Task SlowMovers_Last30Days_OnlyInStockSortedAscendingWithUnsoldFirst()
    {
        var skuW = Guid.NewGuid();
        await StockAsync(_branchA, _skuX, onHand: 5, avgCost: 1);
        await StockAsync(_branchA, _skuY, onHand: 8, avgCost: 1);
        await StockAsync(_branchB, _skuY, onHand: 4, avgCost: 1);
        await StockAsync(_branchA, _skuZ, onHand: 1, avgCost: 1);
        // Hết tồn: không có mặt dù bán ít.
        await StockAsync(_branchA, skuW, onHand: 0, avgCost: 1);
        await SellAsync(_branchA, DaysAgo(2), Line(_skuX, quantity: 6), Line(_skuY, quantity: 1));
        await SellAsync(_branchB, DaysAgo(2), Line(_skuY, quantity: 2));
        // Bán trước cửa sổ không làm SKU Z hết "không bán được gì".
        await SellAsync(_branchA, DaysAgo(45), Line(_skuZ, quantity: 9));

        var slow = await GetAsync<List<SlowMoverRow>>("Staff", "/reports/slow-movers");

        Assert.Equal(
            [(_skuZ, 0, 1), (_skuY, 3, 12), (_skuX, 6, 5)],
            slow.Select(row => (row.SkuId, row.Quantity, row.OnHand)));

        var branchB = await GetAsync<List<SlowMoverRow>>("Owner", $"/reports/slow-movers?branchId={_branchB}");
        Assert.Equal((_skuY, 2, 4), (Assert.Single(branchB).SkuId, branchB[0].Quantity, branchB[0].OnHand));
    }

    [Theory]
    [Trait("UseCase", "UC-REP-02 AC-1")]
    [InlineData("/reports/inventory-value")]
    [InlineData("/reports/top-sellers")]
    [InlineData("/reports/slow-movers")]
    public async Task StockReports_Cashier_Returns403(string path)
    {
        var response = await factory.CreateClient("Cashier", _tenantId).GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/reports/inventory-value?page=0")]
    [InlineData("/reports/inventory-value?pageSize=101")]
    [InlineData("/reports/top-sellers?days=0")]
    [InlineData("/reports/top-sellers?limit=101")]
    [InlineData("/reports/slow-movers?days=366")]
    public async Task StockReports_InvalidQuery_Returns400(string path)
    {
        var response = await factory.CreateClient("Owner", _tenantId).GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_failed", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    // NFR-TENANT-01: tồn và số bán của tenant khác không lẫn vào báo cáo.
    [Fact]
    [Trait("Scenario", "T14")]
    public async Task StockReports_OtherTenantData_NotIncluded()
    {
        var other = Guid.NewGuid();
        await factory.ApplyStockChangeAsync(other, new StockChanged(_branchA, _skuX, 10, 0, 10, 1_000, null, Version: 1));
        await factory.RecordSaleAsync(other, Confirmed(_branchA, DaysAgo(1), "POS", Line(_skuX)));

        Assert.Empty((await GetAsync<InventoryValueReport>("Owner", "/reports/inventory-value")).Items);
        Assert.Empty(await GetAsync<List<TopSellerRow>>("Owner", "/reports/top-sellers"));
        Assert.Empty(await GetAsync<List<SlowMoverRow>>("Owner", "/reports/slow-movers"));
    }

    // Mốc thời gian tương đối so với lúc chạy test, cách xa ranh giới ngày của cửa sổ.
    private static DateTimeOffset DaysAgo(int days) => DateTimeOffset.UtcNow.AddDays(-days);

    private Task StockAsync(Guid branchId, Guid skuId, int onHand, decimal avgCost) =>
        factory.ApplyStockChangeAsync(_tenantId, new StockChanged(branchId, skuId, onHand, 0, onHand, avgCost, null, Version: 1));

    private Task SellAsync(Guid branchId, DateTimeOffset confirmedAt, params OrderConfirmedLine[] lines) =>
        factory.RecordSaleAsync(_tenantId, Confirmed(branchId, confirmedAt, "POS", lines));

    private async Task<T> GetAsync<T>(string role, string path)
    {
        var response = await factory.CreateClient(role, _tenantId).GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
