using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Messaging;
using Oism.Contracts;
using static Oism.Insights.IntegrationTests.TestKit;

namespace Oism.Insights.IntegrationTests.Messaging;

// W4-01: consumer OrderConfirmed, OrderCancelled, StockChanged, SkuUpserted dựng bảng đọc của `insights`.
public sealed class ProjectionConsumerTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private static readonly DateTimeOffset ConfirmedAt = new(2026, 10, 20, 3, 15, 27, TimeSpan.Zero);

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _branchId = Guid.NewGuid();
    private readonly EventBus _bus = new(factory);

    public Task InitializeAsync() => _bus.StartAsync(
        nameof(OrderConfirmed), nameof(OrderCancelled), nameof(StockChanged), nameof(SkuUpserted));

    public async Task DisposeAsync() => await _bus.DisposeAsync();

    // Điều kiện xong của W4-01: phát lại event không cộng trùng. Cùng eventId tới hai lần thì inbox chặn lần sau.
    [Fact]
    [Trait("Scenario", "T22")]
    [Trait("UseCase", "UC-REP-01 AC-5")]
    public async Task OrderConfirmed_SameEventDeliveredTwice_OneSalesFactPerLine()
    {
        var skuId = Guid.NewGuid();
        var order = Confirmed(_branchId, ConfirmedAt, "Shopee", Line(skuId), Line(Guid.NewGuid(), quantity: 1, unitPrice: 90_000, costPrice: 50_000));
        var eventId = Guid.NewGuid();

        await _bus.PublishAsync(order, _tenantId, eventId);
        await _bus.PublishAsync(order, _tenantId, eventId);
        await _bus.DrainAsync(Confirmed(Guid.NewGuid(), ConfirmedAt, "POS", Line(Guid.NewGuid())));

        var facts = await FactsAsync();
        Assert.Equal(2, facts.Count);
        var fact = Assert.Single(facts, fact => fact.SkuId == skuId);
        Assert.Equal(
            (order.Lines[0].OrderItemId, order.OrderId, _branchId, "Shopee", 2, 300_000m, 220_000m, ConfirmedAt, _tenantId),
            (fact.OrderItemId, fact.OrderId, fact.BranchId, fact.Channel, fact.Quantity, fact.Revenue, fact.Cogs, fact.ConfirmedAt,
                fact.TenantId));
        Assert.Equal(1, await factory.QueryAsync(_tenantId, db => db.Set<InboxMessage>().CountAsync(message => message.EventId == eventId)));
    }

    // Cùng đơn được phát lại với eventId khác (ví dụ dựng lại bảng đọc từ đầu): khóa chính order_item_id chặn cộng đôi.
    [Fact]
    [Trait("Scenario", "T22")]
    [Trait("UseCase", "UC-REP-01 AC-5")]
    public async Task OrderConfirmed_ReplayedWithNewEventId_SalesFactsUnchanged()
    {
        var order = Confirmed(_branchId, ConfirmedAt, "TikTok", Line(Guid.NewGuid()));

        await _bus.PublishAsync(order, _tenantId);
        await _bus.PublishAsync(order, _tenantId);
        await _bus.DrainAsync(Confirmed(Guid.NewGuid(), ConfirmedAt, "POS", Line(Guid.NewGuid())));

        var fact = Assert.Single(await FactsAsync());
        Assert.Equal((300_000m, 220_000m), (fact.Revenue, fact.Cogs));
    }

    // Đơn bị hủy không tác động số liệu doanh thu; consumer nhận mà không lỗi.
    [Fact]
    [Trait("UseCase", "UC-REP-01 AC-1")]
    public async Task OrderCancelled_Received_SalesFactsUnchanged()
    {
        await _bus.PublishAsync(Confirmed(_branchId, ConfirmedAt, "Shopee", Line(Guid.NewGuid())), _tenantId);
        await _bus.DrainAsync(Confirmed(Guid.NewGuid(), ConfirmedAt, "POS", Line(Guid.NewGuid())));

        var eventId = await _bus.PublishAsync(
            new OrderCancelled(Guid.NewGuid(), "Shopee", "SP-1", _branchId, "Expired", ConfirmedAt), _tenantId);
        await _bus.DrainAsync(new OrderCancelled(Guid.NewGuid(), "Admin", null, Guid.NewGuid(), "Manual", ConfirmedAt));

        Assert.Single(await FactsAsync());
        Assert.Equal(1, await factory.QueryAsync(_tenantId, db => db.Set<InboxMessage>().CountAsync(message => message.EventId == eventId)));
    }

    // Thông điệp trạng thái tới sai thứ tự: bản có version nhỏ hơn hoặc bằng bản đang giữ bị bỏ qua.
    [Fact]
    public async Task StockChanged_NewerVersionOverwrites_OlderOrEqualVersionIsIgnored()
    {
        var skuId = Guid.NewGuid();
        await _bus.PublishAsync(new StockChanged(_branchId, skuId, 10, 2, 8, 110_000, Threshold: 5, Version: 3), _tenantId);
        await _bus.PublishAsync(new StockChanged(_branchId, skuId, 1, 0, 1, 1, Threshold: null, Version: 2), _tenantId);
        await _bus.PublishAsync(new StockChanged(_branchId, skuId, 7, 7, 0, 1, Threshold: null, Version: 3), _tenantId);
        await _bus.DrainAsync(new StockChanged(Guid.NewGuid(), Guid.NewGuid(), 0, 0, 0, 0, null, 1));

        var snapshot = Assert.Single(await factory.QueryAsync(_tenantId, db => db.StockSnapshots.AsNoTracking().ToListAsync()));
        Assert.Equal((_branchId, skuId, 10, 2, 8, 110_000m, (int?)5, 3L), (
            snapshot.BranchId, snapshot.SkuId, snapshot.OnHand, snapshot.Reserved, snapshot.Available, snapshot.AvgCost,
            snapshot.Threshold, snapshot.Version));

        await _bus.PublishAsync(new StockChanged(_branchId, skuId, 12, 2, 10, 115_000, Threshold: null, Version: 4), _tenantId);
        await _bus.DrainAsync(new StockChanged(Guid.NewGuid(), Guid.NewGuid(), 0, 0, 0, 0, null, 1));

        snapshot = Assert.Single(await factory.QueryAsync(_tenantId, db => db.StockSnapshots.AsNoTracking().ToListAsync()));
        Assert.Equal((12, 10, 115_000m, (int?)null, 4L), (snapshot.OnHand, snapshot.Available, snapshot.AvgCost, snapshot.Threshold, snapshot.Version));
    }

    [Fact]
    public async Task SkuUpserted_NewerVersionOverwrites_OlderOrEqualVersionIsIgnored()
    {
        var created = new SkuUpserted(Guid.NewGuid(), Guid.NewGuid(), "AO-01", "Áo thun, Đen", [], 150_000, 120_000, IsActive: true, Version: 2);

        await _bus.PublishAsync(created, _tenantId);
        await _bus.PublishAsync(created with { Name = "Bản cũ tới muộn", Version = 1 }, _tenantId);
        await _bus.DrainAsync(created with { SkuId = Guid.NewGuid() });

        var sku = Assert.Single(await factory.QueryAsync(_tenantId, db => db.SkuNames.AsNoTracking().ToListAsync()));
        Assert.Equal((created.SkuId, "AO-01", "Áo thun, Đen", 2L), (sku.SkuId, sku.SkuCode, sku.Name, sku.Version));
    }

    // NFR-TENANT-01: consumer đặt tenant context theo thông điệp; bảng đọc chỉ thuộc tenant đó.
    [Fact]
    [Trait("Scenario", "T14")]
    public async Task Projections_FromTwoTenants_EachTenantSeesOnlyItsOwnRows()
    {
        var otherTenantId = Guid.NewGuid();
        await _bus.PublishAsync(Confirmed(_branchId, ConfirmedAt, "Shopee", Line(Guid.NewGuid())), otherTenantId);
        await _bus.PublishAsync(new StockChanged(_branchId, Guid.NewGuid(), 1, 0, 1, 1, null, 1), otherTenantId);
        await _bus.DrainAsync(Confirmed(Guid.NewGuid(), ConfirmedAt, "POS", Line(Guid.NewGuid())));
        await _bus.DrainAsync(new StockChanged(Guid.NewGuid(), Guid.NewGuid(), 0, 0, 0, 0, null, 1));

        Assert.Empty(await FactsAsync());
        Assert.Empty(await factory.QueryAsync(_tenantId, db => db.StockSnapshots.AsNoTracking().ToListAsync()));
        Assert.Equal(otherTenantId, Assert.Single(await factory.QueryAsync(otherTenantId, db => db.SalesFacts.AsNoTracking().ToListAsync())).TenantId);
    }

    private Task<List<Domain.Sales.SalesFact>> FactsAsync() =>
        factory.QueryAsync(_tenantId, db => db.SalesFacts.AsNoTracking().ToListAsync());
}
