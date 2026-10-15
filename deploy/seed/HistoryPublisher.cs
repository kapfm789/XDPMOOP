using System.Text.Json;
using RabbitMQ.Client;

namespace Oism.Seed;

// Đưa đơn lịch sử lên exchange chung như tiến trình đẩy outbox của core vẫn làm (docs/architecture/messaging.md):
// phong bì chung, routing key là tên thông điệp. insights nhận qua consumer thật, ghi inbox theo eventId và
// sales_facts theo order_item_id, nên phát lại cùng đơn không cộng trùng.
internal sealed class HistoryPublisher(string connectionString) : IAsyncDisposable
{
    private const string Exchange = "oism.events";
    private const string Type = "OrderConfirmed";

    private IConnection? _connection;
    private IChannel? _channel;

    public async Task<int> PublishAsync(Guid tenantId, IEnumerable<HistoricalOrder> orders)
    {
        var channel = await OpenAsync();
        var sent = 0;
        foreach (var (eventId, order) in orders)
        {
            var envelope = new
            {
                eventId,
                type = Type,
                tenantId,
                occurredAt = order.ConfirmedAt,
                payload = order,
            };
            // Bật publisher confirm: lời gọi chỉ xong khi broker đã nhận.
            await channel.BasicPublishAsync(
                Exchange, routingKey: Type, mandatory: false,
                new BasicProperties { Persistent = true, MessageId = eventId.ToString(), Type = Type },
                JsonSerializer.SerializeToUtf8Bytes(envelope, GatewayClient.Json));
            sent++;
        }

        return sent;
    }

    private async Task<IChannel> OpenAsync()
    {
        if (_channel is not null)
            return _channel;

        _connection = await new ConnectionFactory { Uri = new Uri(connectionString) }.CreateConnectionAsync("oism-seed");
        _channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true));
        await _channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true);
        // Khai báo trước queue bền của insights, đúng tên và tham số như service, để lịch sử không mất khi insights
        // chưa kịp khởi động và nghe.
        const string queue = $"insights.{Type}";
        await _channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false);
        await _channel.QueueBindAsync(queue, Exchange, routingKey: Type);
        return _channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.DisposeAsync();
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
