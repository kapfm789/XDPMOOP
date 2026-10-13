using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Auth;
using Oism.BuildingBlocks.Messaging;
using Oism.Channel.Domain;
using Oism.Channel.Infrastructure;
using Oism.Contracts;
using RabbitMQ.Client;
using static Oism.Channel.IntegrationTests.TestKit;

namespace Oism.Channel.IntegrationTests.Messaging;

// W3-03: kết quả giữ hàng của `core` về tới `channel` bằng OrderReserved và OrderRejected. Thông điệp được đưa thẳng lên
// exchange như tiến trình đẩy outbox của `core` vẫn làm (docs/architecture/messaging.md).
public sealed class OrderResultConsumerTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private const string Exchange = "oism.events";

    private readonly Guid _tenantId = Guid.NewGuid();
    private IConnection _connection = null!;
    private IChannel _channel = null!;

    private HttpClient Owner => factory.CreateClient(Roles.Owner, _tenantId);

    public async Task InitializeAsync()
    {
        // Khởi động service: chạy migration và consumer.
        factory.CreateClient().Dispose();

        _connection = await new ConnectionFactory { Uri = new Uri(factory.Broker.GetConnectionString()) }.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        // Khai báo queue bền đúng tên và đúng tham số như service, để thông điệp gửi trước khi consumer kịp nghe không bị bỏ.
        await _channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true);
        foreach (var type in new[] { nameof(OrderReserved), nameof(OrderRejected) })
        {
            var queue = $"{ChannelDbContext.Schema}.{type}";
            await _channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false);
            await _channel.QueueBindAsync(queue, Exchange, routingKey: type);
        }
    }

    public async Task DisposeAsync()
    {
        await _channel.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    [Trait("UseCase", "UC-SIM-01 AC-6")]
    public async Task OrderReservedAndRejected_ForSubmittedWebhooks_MoveThemToReservedWithTheOrderOrRejectedWithTheReason()
    {
        var shop = await Owner.CreateShopAsync();
        await factory.SendWebhookAsync("shopee", Shopee("evt-ok", shop.ShopId, "ORD-OK"), shop.Secret!).AcceptedStatusAsync("evt-ok");
        await factory.SendWebhookAsync("shopee", Shopee("evt-no", shop.ShopId, "ORD-NO"), shop.Secret!).AcceptedStatusAsync("evt-no");
        // Hai webhook khác mã sự kiện nhưng cùng một đơn: `core` chỉ nhận một đơn, cả hai bản ghi theo kết quả của đơn đó.
        await factory.SendWebhookAsync("shopee", Shopee("evt-ok-again", shop.ShopId, "ORD-OK"), shop.Secret!).AcceptedStatusAsync("evt-ok-again");
        var (orderId, reservedEvent) = (Guid.NewGuid(), Guid.NewGuid());
        var branch = Guid.NewGuid();

        // Đơn thủ công không có mã đơn của sàn, và đơn của tenant khác trùng mã: không đụng tới bản ghi nào ở đây.
        await PublishAsync(Reserved(Guid.NewGuid(), "Admin", externalOrderId: null, branch));
        await PublishAsync(Reserved(Guid.NewGuid(), "Shopee", "ORD-OK", branch), tenantId: Guid.NewGuid());
        await PublishAsync(Reserved(orderId, "Shopee", "ORD-OK", branch), reservedEvent);
        await PublishAsync(new OrderRejected("Shopee", "ORD-NO", branch, "InsufficientStock", [new OrderRejectedDetail("AO-A-DEN-M", 1, 0)]));
        // Broker giao lại đúng thông điệp đó, rồi một thông điệp khác cho cùng đơn tới sau: inbox bỏ qua bản trùng,
        // và bản ghi đã rời Submitted thì không đổi nữa.
        await PublishAsync(Reserved(orderId, "Shopee", "ORD-OK", branch), reservedEvent);
        await PublishAsync(Reserved(Guid.NewGuid(), "Shopee", "ORD-OK", branch));
        await DrainAsync(shop.ShopId, shop.Secret!);

        var webhooks = (await factory.WebhooksAsync(_tenantId)).ToDictionary(webhook => webhook.EventId);
        Assert.Equal(
            (WebhookStatus.Reserved, (Guid?)orderId, (string?)null),
            (webhooks["evt-ok"].Status, webhooks["evt-ok"].OrderId, webhooks["evt-ok"].Reason));
        Assert.Equal((WebhookStatus.Reserved, (Guid?)orderId), (webhooks["evt-ok-again"].Status, webhooks["evt-ok-again"].OrderId));
        Assert.Equal(
            (WebhookStatus.Rejected, (Guid?)null, "InsufficientStock"),
            (webhooks["evt-no"].Status, webhooks["evt-no"].OrderId, webhooks["evt-no"].Reason));
        Assert.True(webhooks["evt-ok"].UpdatedAt > webhooks["evt-ok"].ReceivedAt);
        Assert.Equal(1, await factory.QueryAsync(_tenantId, db => db.Set<InboxMessage>().CountAsync(message => message.EventId == reservedEvent)));
        // Sàn gửi lại webhook sau khi đã có kết quả: 202 với trạng thái hiện có (UC-SIM-01 AC-4).
        Assert.Equal("Reserved", await factory.SendWebhookAsync("shopee", Shopee("evt-ok", shop.ShopId, "ORD-OK"), shop.Secret!).AcceptedStatusAsync("evt-ok"));
        Assert.Equal("Rejected", await factory.SendWebhookAsync("shopee", Shopee("evt-no", shop.ShopId, "ORD-NO"), shop.Secret!).AcceptedStatusAsync("evt-no"));

        // Một webhook mới, khác mã sự kiện, cho đơn đã được giữ hàng: `core` sẽ bỏ qua đơn trùng mà không phát gì,
        // nên bản ghi nhận luôn kết quả đã biết. Đơn từng bị từ chối thì được gửi lại cho `core` xét.
        var late = await factory.SendWebhookAsync("shopee", Shopee("evt-ok-late", shop.ShopId, "ORD-OK"), shop.Secret!).AcceptedStatusAsync("evt-ok-late");
        var retry = await factory.SendWebhookAsync("shopee", Shopee("evt-no-retry", shop.ShopId, "ORD-NO"), shop.Secret!).AcceptedStatusAsync("evt-no-retry");

        Assert.Equal(("Reserved", "Submitted"), (late, retry));
        webhooks = (await factory.WebhooksAsync(_tenantId)).ToDictionary(webhook => webhook.EventId);
        Assert.Equal((WebhookStatus.Reserved, (Guid?)orderId), (webhooks["evt-ok-late"].Status, webhooks["evt-ok-late"].OrderId));
        Assert.Equal((WebhookStatus.Submitted, (Guid?)null), (webhooks["evt-no-retry"].Status, webhooks["evt-no-retry"].OrderId));
        // Command vẫn được ghi cho cả hai: hàng rào ở `core` mới là nơi quyết định đơn trùng.
        Assert.Equal(3, (await factory.OutboxAsync<SubmitOrder>(_tenantId)).Count(command => command.ExternalOrderId == "ORD-OK"));
        Assert.Equal(2, (await factory.OutboxAsync<SubmitOrder>(_tenantId)).Count(command => command.ExternalOrderId == "ORD-NO"));
    }

    private static OrderReserved Reserved(Guid orderId, string channel, string? externalOrderId, Guid branchId) => new(
        orderId, "DH000000000001", channel, externalOrderId, branchId, 150_000, DateTimeOffset.UtcNow.AddMinutes(30),
        [new OrderReservedLine(Guid.NewGuid(), 1)]);

    // Phong bì chung, routing key là tên thông điệp.
    private async Task PublishAsync<TPayload>(TPayload payload, Guid? eventId = null, Guid? tenantId = null)
    {
        var envelope = new EventEnvelope(
            eventId ?? Guid.NewGuid(), typeof(TPayload).Name, tenantId ?? _tenantId, DateTimeOffset.UtcNow,
            JsonSerializer.SerializeToElement(payload, EventEnvelope.JsonOptions));
        await _channel.BasicPublishAsync(
            Exchange, routingKey: typeof(TPayload).Name, mandatory: false, new BasicProperties { Persistent = true },
            JsonSerializer.SerializeToUtf8Bytes(envelope, EventEnvelope.JsonOptions));
    }

    // Consumer chạy nền thật nên test chờ kết quả thay vì điều khiển đồng hồ. Một queue xử lý lần lượt từng thông điệp:
    // khi hai webhook chốt (gửi sau cùng, mỗi queue một cái) đã có kết quả thì mọi thông điệp trước chúng đã được xử lý.
    private async Task DrainAsync(string shopId, string secret)
    {
        await factory.SendWebhookAsync("shopee", Shopee("marker-ok", shopId, "MARKER-OK"), secret).AcceptedStatusAsync("marker-ok");
        await factory.SendWebhookAsync("shopee", Shopee("marker-no", shopId, "MARKER-NO"), secret).AcceptedStatusAsync("marker-no");
        await PublishAsync(Reserved(Guid.NewGuid(), "Shopee", "MARKER-OK", Guid.NewGuid()));
        await PublishAsync(new OrderRejected("Shopee", "MARKER-NO", Guid.NewGuid(), "UnknownSku", []));

        var deadline = DateTime.UtcNow.AddSeconds(60);
        while ((await factory.WebhooksAsync(_tenantId)).Count(webhook =>
            webhook.EventId.StartsWith("marker-") && webhook.Status != WebhookStatus.Submitted) < 2)
        {
            Assert.True(DateTime.UtcNow < deadline, "consumers never handled the marker messages");
            await Task.Delay(100);
        }

        // Bản trùng, bản tới muộn và bản không có bản ghi nào để đổi được bỏ qua, không bị coi là lỗi.
        foreach (var type in new[] { nameof(OrderReserved), nameof(OrderRejected) })
            Assert.Equal(0u, (await _channel.QueueDeclarePassiveAsync($"{ChannelDbContext.Schema}.{type}.error")).MessageCount);
    }
}
