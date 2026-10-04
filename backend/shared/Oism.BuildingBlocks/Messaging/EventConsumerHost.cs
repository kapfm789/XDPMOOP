using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oism.BuildingBlocks.Persistence;
using Oism.BuildingBlocks.Tenancy;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Oism.BuildingBlocks.Messaging;

// Nhận thông điệp cho mọi consumer đã đăng ký: docs/architecture/messaging.md mục "Phía nhận".
internal sealed class EventConsumerHost(
    IServiceScopeFactory scopes,
    IEnumerable<ConsumerRegistration> consumers,
    IConfiguration configuration,
    TimeProvider time,
    ILogger<EventConsumerHost> logger) : BackgroundService
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(2);

    private IConnection? _connection;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!consumers.Any())
            return;

        if (string.IsNullOrWhiteSpace(RabbitMq.Uri(configuration)))
        {
            logger.LogWarning("ConnectionStrings:RabbitMq is empty; no message will be consumed");
            return;
        }

        // Kết nối xong thì RabbitMQ.Client tự nối lại và tự khai báo lại queue khi broker khởi động lại.
        while (!await TrySubscribeAsync(stoppingToken))
            await Task.Delay(ReconnectDelay, stoppingToken);
    }

    private async Task<bool> TrySubscribeAsync(CancellationToken ct)
    {
        try
        {
            _connection = await RabbitMq.ConnectAsync(configuration, automaticRecovery: true, ct);
            var channel = await _connection.CreateChannelAsync(cancellationToken: ct);
            await RabbitMq.DeclareExchangeAsync(channel, ct);
            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, ct);

            // Tên service là schema mặc định của DbContext, ví dụ core.
            string service;
            await using (var scope = scopes.CreateAsyncScope())
                service = scope.ServiceProvider.GetRequiredService<OismDbContext>().Model.GetDefaultSchema()!;

            foreach (var consumer in consumers)
                await SubscribeAsync(channel, $"{service}.{consumer.MessageType}", consumer, ct);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not subscribe to RabbitMQ; retrying");
            if (_connection is not null)
                await _connection.DisposeAsync();
            _connection = null;
            return false;
        }
    }

    private async Task SubscribeAsync(IChannel channel, string queue, ConsumerRegistration registration, CancellationToken ct)
    {
        var errorQueue = $"{queue}.error";
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueDeclareAsync(errorQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync(queue, RabbitMq.Exchange, routingKey: registration.MessageType, cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            if (!await TryHandleAsync(registration, delivery.Body, delivery.CancellationToken))
            {
                await channel.BasicPublishAsync(
                    exchange: "", routingKey: errorQueue, mandatory: false,
                    new BasicProperties { Persistent = true }, delivery.Body, delivery.CancellationToken);
            }

            // Xác nhận sau khi đã commit. Mất kết nối trước bước này thì broker giao lại và inbox bỏ qua bản trùng.
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, delivery.CancellationToken);
        };
        await channel.BasicConsumeAsync(queue, autoAck: false, consumer, ct);
    }

    // False khi đã thử lại đủ số lần mà vẫn lỗi: thông điệp sang queue lỗi để không chặn các thông điệp sau.
    private async Task<bool> TryHandleAsync(ConsumerRegistration registration, ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var envelope = JsonSerializer.Deserialize<EventEnvelope>(body.Span, EventEnvelope.JsonOptions)!;
                await HandleAsync(registration, envelope, ct);
                return true;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                if (attempt == MaxRetries)
                {
                    logger.LogError(exception, "Giving up on {MessageType} after {Retries} retries", registration.MessageType, MaxRetries);
                    return false;
                }

                logger.LogWarning(exception, "Handling {MessageType} failed; retry {Attempt}", registration.MessageType, attempt + 1);
                await Task.Delay(RetryDelay * (attempt + 1), ct);
            }
        }
    }

    private async Task HandleAsync(ConsumerRegistration registration, EventEnvelope envelope, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(envelope.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<OismDbContext>();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (await db.Set<InboxMessage>().AnyAsync(message => message.EventId == envelope.EventId, ct))
            return;

        // Ghi inbox trước khi xử lý: bản trùng tới cùng lúc sẽ vấp khóa chính, lỗi, rồi được bỏ qua ở lần thử lại.
        db.Add(new InboxMessage(envelope.EventId, envelope.Type, time.GetUtcNow()));
        await db.SaveChangesAsync(ct);

        await registration.HandleAsync(scope.ServiceProvider, envelope, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
