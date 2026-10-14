using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Oism.Contracts;

namespace Oism.Insights.IntegrationTests.Realtime;

// W4-04, UC-SIM-02, UC-REP-03 AC-1, AC-4: hub /hubs/notifications, group theo tenant và chi nhánh. Thông điệp đi qua
// RabbitMQ thật tới consumer, thông báo đi qua WebSocket thật tới client SignalR.
public sealed class NotificationsHubTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _branchX = Guid.NewGuid();
    private readonly Guid _branchY = Guid.NewGuid();
    private readonly EventBus _bus = new(factory);
    private readonly List<Session> _sessions = [];

    public Task InitializeAsync() => _bus.StartAsync(nameof(OrderReserved), nameof(StockChanged), nameof(SkuUpserted));

    public async Task DisposeAsync()
    {
        foreach (var session in _sessions)
            await session.Connection.DisposeAsync();
        await _bus.DisposeAsync();
    }

    // T15, điều kiện xong của W4-04: phiên của tenant khác không nhận thông báo.
    [Fact]
    [Trait("Scenario", "T15")]
    [Trait("UseCase", "UC-SIM-02 AC-1")]
    [Trait("UseCase", "UC-SIM-02 AC-3")]
    public async Task OrderCreated_OrderOfTenantA_OnlySessionOfTenantAReceivesIt()
    {
        var ownerA = await ConnectAsync("Owner", _tenantA);
        var ownerB = await ConnectAsync("Owner", _tenantB);

        var order = Reserved(_branchX, "SO-0001");
        await _bus.PublishAsync(order, _tenantA);
        // Thông điệp chốt của B gửi sau cùng trên cùng queue: B nhận được nó thì đơn của A đã được xử lý xong.
        var marker = Reserved(_branchX, "SO-B-0001");
        await _bus.PublishAsync(marker, _tenantB);
        await ownerB.WaitForAsync("OrderCreated", marker.OrderId);
        await ownerA.WaitForAsync("OrderCreated", order.OrderId);

        var received = Assert.Single(ownerA.Received);
        Assert.Equal("OrderCreated", received.Name);
        Assert.Equal(
            (order.OrderId, "SO-0001", "Shopee", _branchX, 450_000m),
            (received.Payload.GetProperty("orderId").GetGuid(), received.Payload.GetProperty("orderNumber").GetString(),
                received.Payload.GetProperty("channel").GetString(), received.Payload.GetProperty("branchId").GetGuid(),
                received.Payload.GetProperty("totalAmount").GetDecimal()));
        Assert.Equal(marker.OrderId, Assert.Single(ownerB.Received).Payload.GetProperty("orderId").GetGuid());
    }

    // Người dùng gắn chi nhánh chỉ nhận thông báo của chi nhánh đó; Owner và Staff nhận của mọi chi nhánh, mỗi đơn một lần.
    [Fact]
    [Trait("UseCase", "UC-SIM-02 AC-2")]
    public async Task OrderCreated_BranchUser_OnlyOwnBranchWhileStaffGetsEveryBranchOnce()
    {
        var cashierX = await ConnectAsync("Cashier", _tenantA, _branchX);
        var cashierY = await ConnectAsync("Cashier", _tenantA, _branchY);
        // Staff có branch_id vẫn nhận của mọi chi nhánh, và không nhận trùng.
        var staff = await ConnectAsync("Staff", _tenantA, _branchX);

        var orderAtX = Reserved(_branchX, "SO-X");
        var orderAtY = Reserved(_branchY, "SO-Y");
        await _bus.PublishAsync(orderAtX, _tenantA);
        await _bus.PublishAsync(orderAtY, _tenantA);
        await cashierY.WaitForAsync("OrderCreated", orderAtY.OrderId);
        await staff.WaitForAsync("OrderCreated", orderAtY.OrderId);
        await cashierX.WaitForAsync("OrderCreated", orderAtX.OrderId);

        Assert.Equal([orderAtX.OrderId], cashierX.OrderIds());
        Assert.Equal([orderAtY.OrderId], cashierY.OrderIds());
        Assert.Equal([orderAtX.OrderId, orderAtY.OrderId], staff.OrderIds());
    }

    // UC-REP-03 AC-1: LowStock chỉ phát khi available đi từ trên ngưỡng xuống bằng hoặc dưới ngưỡng; AC-4: không tới
    // tenant khác. Cashier không ở group của tenant nên không nhận cảnh báo tồn.
    [Fact]
    [Trait("Scenario", "T15")]
    [Trait("UseCase", "UC-REP-03 AC-1")]
    [Trait("UseCase", "UC-REP-03 AC-4")]
    public async Task LowStock_AvailableCrossesThreshold_PushedOncePerCrossingToTenantOnly()
    {
        var skuId = Guid.NewGuid();
        await _bus.PublishAsync(new SkuUpserted(skuId, Guid.NewGuid(), "AO-01", "Áo thun, Đen", [], 150_000, 120_000, true, 1), _tenantA);
        await _bus.DrainAsync(new SkuUpserted(Guid.NewGuid(), Guid.NewGuid(), "MARKER", "Chốt", [], 0, 0, true, 1));
        var owner = await ConnectAsync("Owner", _tenantA);
        var cashier = await ConnectAsync("Cashier", _tenantA, _branchX);
        var ownerB = await ConnectAsync("Owner", _tenantB);

        await _bus.PublishAsync(Stock(skuId, available: 6, version: 1), _tenantA);
        await _bus.PublishAsync(Stock(skuId, available: 5, version: 2), _tenantA);
        await _bus.PublishAsync(Stock(skuId, available: 4, version: 3), _tenantA);
        await _bus.PublishAsync(Stock(skuId, available: 10, version: 4), _tenantA);
        await _bus.PublishAsync(Stock(skuId, available: 3, version: 5), _tenantA);
        // Bản cũ tới muộn bị bỏ qua, không phát thêm.
        await _bus.PublishAsync(Stock(skuId, available: 0, version: 4), _tenantA);
        var markerSku = Guid.NewGuid();
        await _bus.PublishAsync(Stock(markerSku, available: 0, version: 1), _tenantA);
        await owner.WaitForAsync("LowStock", markerSku);

        var alerts = owner.Received.Select(message => message.Payload).ToList();
        Assert.Equal(
            [(skuId, 5), (skuId, 3), (markerSku, 0)],
            alerts.Select(alert => (alert.GetProperty("skuId").GetGuid(), alert.GetProperty("available").GetInt32())));
        Assert.Equal(
            (_branchX, "AO-01", "Áo thun, Đen", 5),
            (alerts[0].GetProperty("branchId").GetGuid(), alerts[0].GetProperty("skuCode").GetString(),
                alerts[0].GetProperty("name").GetString(), alerts[0].GetProperty("threshold").GetInt32()));
        Assert.Empty(ownerB.Received);
        Assert.Empty(cashier.Received);
    }

    // UC-SIM-02 AC-5: không có token thì kết nối bị từ chối.
    [Fact]
    [Trait("UseCase", "UC-SIM-02 AC-5")]
    public async Task Connect_WithoutToken_Rejected()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => ConnectAsync(token: null));
    }

    private Task<Session> ConnectAsync(string role, Guid tenantId, Guid? branchId = null) =>
        ConnectAsync(factory.CreateToken(role, tenantId, branchId));

    // Như trình duyệt: WebSocket không mang được header, token đi trên query `access_token`.
    private async Task<Session> ConnectAsync(string? token)
    {
        factory.CreateClient().Dispose();
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/notifications"), options =>
            {
                options.SkipNegotiation = true;
                options.Transports = HttpTransportType.WebSockets;
                options.WebSocketFactory = async (context, ct) =>
                {
                    var uri = new UriBuilder(context.Uri) { Scheme = "http", Query = token is null ? "" : $"access_token={token}" }.Uri;
                    return await factory.Server.CreateWebSocketClient().ConnectAsync(uri, ct);
                };
            })
            .Build();

        var session = new Session(connection);
        connection.On<JsonElement>("OrderCreated", payload => session.Received.Enqueue(("OrderCreated", payload)));
        connection.On<JsonElement>("LowStock", payload => session.Received.Enqueue(("LowStock", payload)));
        await connection.StartAsync();
        _sessions.Add(session);
        return session;
    }

    private static OrderReserved Reserved(Guid branchId, string orderNumber) => new(
        Guid.NewGuid(), orderNumber, "Shopee", "EXT-" + orderNumber, branchId, 450_000, DateTimeOffset.UtcNow.AddMinutes(30),
        [new OrderReservedLine(Guid.NewGuid(), 3)]);

    private StockChanged Stock(Guid skuId, int available, long version) =>
        new(_branchX, skuId, available, 0, available, 110_000, Threshold: 5, version);

    private sealed class Session(HubConnection connection)
    {
        public HubConnection Connection { get; } = connection;

        public ConcurrentQueue<(string Name, JsonElement Payload)> Received { get; } = new();

        public List<Guid> OrderIds() =>
            Received.Where(message => message.Name == "OrderCreated").Select(message => message.Payload.GetProperty("orderId").GetGuid()).ToList();

        // Chờ thông báo mang orderId hoặc skuId đã cho.
        public async Task WaitForAsync(string name, Guid id)
        {
            var property = name == "OrderCreated" ? "orderId" : "skuId";
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (!Received.Any(message => message.Name == name && message.Payload.GetProperty(property).GetGuid() == id))
            {
                Assert.True(DateTime.UtcNow < deadline, $"never received {name} for {id}");
                await Task.Delay(50);
            }
        }
    }
}
