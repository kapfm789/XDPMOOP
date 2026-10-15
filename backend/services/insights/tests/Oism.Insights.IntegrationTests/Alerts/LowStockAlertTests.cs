using System.Net;
using System.Net.Http.Json;
using Oism.Contracts;
using Oism.Insights.Application.Alerts.ListLowStock;

namespace Oism.Insights.IntegrationTests.Alerts;

// W4-03, UC-REP-03: GET /alerts/low-stock dựng từ stock_snapshots. Thông báo LowStock qua SignalR có ở NotificationsHubTests.
public sealed class LowStockAlertTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _branchA = Guid.NewGuid();
    private readonly Guid _branchB = Guid.NewGuid();
    private readonly Guid _skuX = Guid.NewGuid();
    private readonly Guid _skuY = Guid.NewGuid();

    // Điều kiện xong của W4-03: hạ tồn xuống ngưỡng thì cảnh báo xuất hiện; lên lại trên ngưỡng thì rời danh sách.
    [Fact]
    [Trait("UseCase", "UC-REP-03 AC-1")]
    [Trait("UseCase", "UC-REP-03 AC-2")]
    public async Task LowStock_AvailableDropsToThresholdThenRises_AppearsThenLeaves()
    {
        await factory.UpsertSkuNameAsync(_tenantId, _skuX, "AO-01", "Áo thun, Đen");
        await StockAsync(_branchA, _skuX, available: 10, threshold: 5, version: 1);
        Assert.Empty(await ListAsync("Owner"));

        await StockAsync(_branchA, _skuX, available: 5, threshold: 5, version: 2);
        var alert = Assert.Single(await ListAsync("Staff"));
        Assert.Equal((_branchA, _skuX, "AO-01", "Áo thun, Đen", 5, 5), (alert.BranchId, alert.SkuId, alert.SkuCode, alert.Name, alert.Available, alert.Threshold));

        await StockAsync(_branchA, _skuX, available: 6, threshold: 5, version: 3);
        Assert.Empty(await ListAsync("Owner"));
    }

    [Fact]
    [Trait("UseCase", "UC-REP-03 AC-3")]
    public async Task LowStock_NoThreshold_NeverAlerts()
    {
        await StockAsync(_branchA, _skuX, available: 0, threshold: null, version: 1);

        Assert.Empty(await ListAsync("Owner"));
    }

    // Lọc theo chi nhánh; danh sách xếp tồn khả dụng tăng dần.
    [Fact]
    [Trait("UseCase", "UC-REP-03 AC-1")]
    public async Task LowStock_BranchFilter_OnlyThatBranchSortedByAvailable()
    {
        await StockAsync(_branchA, _skuX, available: 3, threshold: 5, version: 1);
        await StockAsync(_branchA, _skuY, available: 0, threshold: 2, version: 1);
        await StockAsync(_branchB, _skuX, available: 1, threshold: 5, version: 1);

        Assert.Equal(
            [(_branchA, _skuY), (_branchB, _skuX), (_branchA, _skuX)],
            (await ListAsync("Owner")).Select(row => (row.BranchId, row.SkuId)));
        Assert.Equal(
            [_skuY, _skuX],
            (await ListAsync("Owner", $"?branchId={_branchA}")).Select(row => row.SkuId));
    }

    // T15 phía API: cảnh báo của tenant khác không tới người dùng của tenant này.
    [Fact]
    [Trait("UseCase", "UC-REP-03 AC-4")]
    [Trait("Scenario", "T14")]
    public async Task LowStock_OtherTenantAlert_NotListed()
    {
        await factory.ApplyStockChangeAsync(Guid.NewGuid(), new StockChanged(_branchA, _skuX, 0, 0, 0, 0, 5, Version: 1));

        Assert.Empty(await ListAsync("Owner"));
    }

    [Fact]
    public async Task LowStock_Cashier_Returns403()
    {
        var response = await factory.CreateClient("Cashier", _tenantId).GetAsync("/alerts/low-stock");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private Task StockAsync(Guid branchId, Guid skuId, int available, int? threshold, long version) =>
        factory.ApplyStockChangeAsync(
            _tenantId, new StockChanged(branchId, skuId, available, 0, available, 100_000, threshold, version));

    private async Task<List<LowStockAlertRow>> ListAsync(string role, string query = "")
    {
        var response = await factory.CreateClient(role, _tenantId).GetAsync($"/alerts/low-stock{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<LowStockAlertRow>>())!;
    }
}
