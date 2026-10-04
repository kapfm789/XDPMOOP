using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Auth;
using Oism.BuildingBlocks.Messaging;
using Oism.Contracts;
using Oism.Core.Application.Orders;
using Oism.Core.Infrastructure;
using RabbitMQ.Client;
using static Oism.Core.IntegrationTests.TestKit;

namespace Oism.Core.IntegrationTests.Messaging;

// Một queue của test gắn vào exchange để thấy những gì `core` thật sự phát ra, như `channel` và `insights` sẽ nhận.
internal sealed class EventProbe(IConnection connection, IChannel channel, string queue) : IAsyncDisposable
{
    public const string Exchange = "oism.events";

    private readonly List<EventEnvelope> _seen = [];

    public IChannel Channel => channel;

    public static async Task<EventProbe> StartAsync(ApiFactory factory, params string[] types)
    {
        var connection = await new ConnectionFactory { Uri = new Uri(factory.Broker.GetConnectionString()) }.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true);
        var queue = (await channel.QueueDeclareAsync()).QueueName;
        foreach (var type in types)
            await channel.QueueBindAsync(queue, Exchange, routingKey: type);
        return new EventProbe(connection, channel, queue);
    }

    // Mọi thông điệp của tenant đã tới queue của test cho tới lúc gọi.
    public async Task<IReadOnlyList<EventEnvelope>> SeenAsync(Guid tenantId)
    {
        while (await channel.BasicGetAsync(queue, autoAck: true) is { } delivery)
            _seen.Add(JsonSerializer.Deserialize<EventEnvelope>(delivery.Body.Span, EventEnvelope.JsonOptions)!);
        return _seen.Where(envelope => envelope.TenantId == tenantId).ToList();
    }

    public async ValueTask DisposeAsync()
    {
        await channel.DisposeAsync();
        await connection.DisposeAsync();
    }
}

internal static class Waiting
{
    // Tiến trình đẩy outbox và consumer chạy nền thật, nên test chờ kết quả thay vì điều khiển đồng hồ.
    public static async Task UntilAsync(Func<Task<bool>> condition, string failure)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (!await condition())
        {
            Assert.True(DateTime.UtcNow < deadline, failure);
            await Task.Delay(100);
        }
    }

    public static Task<List<OutboxMessage>> OutboxRowsAsync(this ApiFactory factory, Guid tenantId) =>
        factory.QueryAsync(tenantId, db => db.Set<OutboxMessage>().AsNoTracking().ToListAsync());
}

// W2-05, W2-06: đơn online đi từ RabbitMQ qua consumer SubmitOrder; kết quả giữ hàng đi ra bằng outbox.
public sealed class OrderEventsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    [Trait("Scenario", "T03")]
    [Trait("UseCase", "UC-ORD-01 AC-1")]
    [Trait("UseCase", "UC-ORD-01 AC-4")]
    public async Task SubmitOrder_DeliveredThroughTheBrokerMoreThanOnce_ReservesOnceAndPublishesOrderReserved()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(sku, 5));
        await using var probe = await StartProbeAsync();
        var message = Online("SP-2001", branch, new SubmitOrderLine("AO-01", 2, 150_000, 0));
        var eventId = Guid.NewGuid();

        // RabbitMQ giao lại cùng thông điệp (inbox chặn), rồi một webhook khác mang cùng mã đơn (chỉ mục unique chặn).
        await PublishAsync(probe, message, _tenantId, eventId);
        await PublishAsync(probe, message, _tenantId, eventId);
        await PublishAsync(probe, message, _tenantId, Guid.NewGuid());
        await DrainAsync(probe);

        var order = Assert.Single(await factory.OrdersAsync(_tenantId));
        Assert.Equal(("SP-2001", _tenantId), (order.ExternalOrderId, order.TenantId));
        Assert.Equal(2, (await factory.BalanceAsync(_tenantId, branch, sku))!.Reserved);
        Assert.Equal(1, await factory.QueryAsync(_tenantId, db => db.Set<InboxMessage>().CountAsync(inbox => inbox.EventId == eventId)));
        Assert.Empty(await factory.OutboxAsync<OrderRejected>(_tenantId));

        // Thông điệp trong outbox tới được exchange, trong phong bì chung mang tenant của đơn.
        await Waiting.UntilAsync(
            async () => (await factory.OutboxRowsAsync(_tenantId)).All(row => row.ProcessedAt is not null), "outbox was never dispatched");
        var published = await probe.SeenAsync(_tenantId);
        var reserved = Assert.Single(published, envelope => envelope.Type == nameof(OrderReserved));
        Assert.Equal(order.Id, reserved.Payload.Deserialize<OrderReserved>(EventEnvelope.JsonOptions)!.OrderId);
        Assert.Contains(published, envelope => envelope.Type == nameof(StockChanged)
            && envelope.Payload.Deserialize<StockChanged>(EventEnvelope.JsonOptions)! is { Reserved: 2, OnHand: 5, Available: 3 });
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    // Đơn bị từ chối qua consumer: phần giữ hàng rollback, còn dòng inbox và OrderRejected được commit cùng nhau.
    [Fact]
    [Trait("Scenario", "T04")]
    [Trait("UseCase", "UC-ORD-01 AC-3")]
    public async Task SubmitOrder_DeliveredThroughTheBrokerWithoutEnoughStock_SavesNoOrderAndPublishesOrderRejected()
    {
        var branch = await factory.SeedBranchAsync(_tenantId);
        var sku = await factory.SeedSkuAsync(_tenantId, "AO-01");
        await factory.PostLedgerAsync(_tenantId, branch, In(sku, 1));
        await using var probe = await StartProbeAsync();
        var eventId = Guid.NewGuid();

        await PublishAsync(probe, Online("SP-2002", branch, new SubmitOrderLine("AO-01", 3, 150_000, 0)), _tenantId, eventId);
        await DrainAsync(probe);

        Assert.Empty(await factory.OrdersAsync(_tenantId));
        Assert.Empty(await factory.ReservationsAsync(_tenantId));
        Assert.Equal(0, (await factory.BalanceAsync(_tenantId, branch, sku))!.Reserved);
        Assert.Equal(1, await factory.QueryAsync(_tenantId, db => db.Set<InboxMessage>().CountAsync(inbox => inbox.EventId == eventId)));
        var rejected = Assert.Single(await factory.OutboxAsync<OrderRejected>(_tenantId));
        Assert.Equal(("SP-2002", "InsufficientStock", new OrderRejectedDetail("AO-01", 3, 1)),
            (rejected.ExternalOrderId, rejected.Reason, Assert.Single(rejected.Details)));
        await Waiting.UntilAsync(
            async () => (await probe.SeenAsync(_tenantId)).Any(envelope => envelope.Type == nameof(OrderRejected)), "OrderRejected never reached the exchange");
        await factory.AssertInventoryInvariantsAsync(_tenantId);
    }

    private async Task<EventProbe> StartProbeAsync()
    {
        // Khởi động service: chạy migration, consumer và tiến trình đẩy outbox.
        factory.CreateClient().Dispose();
        var probe = await EventProbe.StartAsync(factory, nameof(OrderReserved), nameof(OrderRejected), nameof(StockChanged));
        // Khai báo queue bền đúng tên và đúng tham số như service, để thông điệp gửi trước khi consumer kịp nghe không bị bỏ.
        var queue = $"{CoreDbContext.Schema}.{nameof(SubmitOrder)}";
        await probe.Channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false);
        await probe.Channel.QueueBindAsync(queue, EventProbe.Exchange, routingKey: nameof(SubmitOrder));
        return probe;
    }

    private static async Task PublishAsync(EventProbe probe, SubmitOrder message, Guid tenantId, Guid eventId)
    {
        var envelope = new EventEnvelope(
            eventId, nameof(SubmitOrder), tenantId, DateTimeOffset.UtcNow, JsonSerializer.SerializeToElement(message, EventEnvelope.JsonOptions));
        await probe.Channel.BasicPublishAsync(
            EventProbe.Exchange, routingKey: nameof(SubmitOrder), mandatory: false, new BasicProperties { Persistent = true },
            JsonSerializer.SerializeToUtf8Bytes(envelope, EventEnvelope.JsonOptions));
    }

    // Một queue xử lý lần lượt từng thông điệp: khi đơn chốt (gửi sau cùng, thuộc một tenant riêng, mang SKU không tồn tại)
    // đã bị từ chối thì mọi thông điệp trước nó đã được xử lý.
    private async Task DrainAsync(EventProbe probe)
    {
        var markerTenantId = Guid.NewGuid();
        await PublishAsync(probe, Online("MARKER", Guid.NewGuid(), new SubmitOrderLine("MARKER", 1, 1, 0)), markerTenantId, Guid.NewGuid());
        await Waiting.UntilAsync(
            async () => (await factory.OutboxAsync<OrderRejected>(markerTenantId)).Count > 0, "consumer never handled the SubmitOrder marker");

        // Bản trùng và đơn bị từ chối được xử lý xong, không bị coi là lỗi.
        Assert.Equal(0u, (await probe.Channel.QueueDeclarePassiveAsync($"{CoreDbContext.Schema}.{nameof(SubmitOrder)}.error")).MessageCount);
    }
}

// Điều kiện xong của W2-06 (NFR-SEC-02): event chỉ phát khi transaction commit; RabbitMQ tắt vẫn commit được.
public sealed class OutboxWithoutBrokerTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly Guid _tenantId = Guid.NewGuid();

    public OutboxWithoutBrokerTests(ApiFactory factory)
    {
        _factory = factory;
        // Không có broker nào nghe ở cổng 1: với service, RabbitMQ đang tắt.
        factory.RabbitMqUri = "amqp://guest:guest@localhost:1";
    }

    // T23 ghi "duyệt một đơn"; bước duyệt thuộc W3-01, nên ở phase 4 kịch bản chạy trên bước tạo đơn và giữ hàng.
    [Fact]
    [Trait("Scenario", "T23")]
    public async Task CreateOrder_BrokerDownAtCommit_CommitsAndSendsEachMessageOnceWhenTheBrokerIsBack()
    {
        var branch = await _factory.SeedBranchAsync(_tenantId);
        var sku = await _factory.SeedSkuAsync(_tenantId, "AO-01");
        await _factory.PostLedgerAsync(_tenantId, branch, In(sku, 5));

        var response = await _factory.CreateClient(Roles.Staff, _tenantId)
            .PostAsJsonAsync("/orders", new { branchId = branch, items = new[] { new { skuId = sku, quantity = 2 } } });

        // Transaction commit dù không gửi được gì: đơn đã giữ hàng, thông điệp nằm chờ trong outbox.
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = (await response.Content.ReadFromJsonAsync<OrderDto>())!;
        Assert.Equal("Reserved", order.Status);
        Assert.Equal(2, (await _factory.BalanceAsync(_tenantId, branch, sku))!.Reserved);
        await Waiting.UntilAsync(
            async () => (await _factory.OutboxRowsAsync(_tenantId)).Any(row => row.Attempts > 0), "dispatcher never tried to send");
        var waiting = await _factory.OutboxRowsAsync(_tenantId);
        Assert.All(waiting, row => Assert.Null(row.ProcessedAt));
        Assert.Single(waiting, row => row.Type == nameof(OrderReserved));

        // RabbitMQ chạy lại.
        await using var probe = await EventProbe.StartAsync(_factory, nameof(OrderReserved), nameof(StockChanged));
        _factory.Services.GetRequiredService<IConfiguration>()["ConnectionStrings:RabbitMq"] = _factory.Broker.GetConnectionString();

        await Waiting.UntilAsync(
            async () => (await _factory.OutboxRowsAsync(_tenantId)).All(row => row.ProcessedAt is not null), "outbox was never dispatched");
        var published = await probe.SeenAsync(_tenantId);
        // Mỗi dòng outbox được gửi đúng một lượt, mang eventId là id của dòng đó.
        Assert.Equal(waiting.Select(row => row.Id).Order(), published.Select(envelope => envelope.EventId).Order());
        var reserved = Assert.Single(published, envelope => envelope.Type == nameof(OrderReserved));
        Assert.Equal(order.Id, reserved.Payload.Deserialize<OrderReserved>(EventEnvelope.JsonOptions)!.OrderId);
        await _factory.AssertInventoryInvariantsAsync(_tenantId);
    }
}
