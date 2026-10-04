using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oism.BuildingBlocks.Persistence;
using RabbitMQ.Client;

namespace Oism.BuildingBlocks.Messaging;

// Tiến trình đẩy outbox: docs/architecture/messaging.md mục "Phía phát".
internal sealed class OutboxDispatcher(
    IServiceScopeFactory scopes, IConfiguration configuration, TimeProvider time, ILogger<OutboxDispatcher> logger)
    : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private IConnection? _connection;
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(RabbitMq.Uri(configuration)))
        {
            logger.LogWarning("ConnectionStrings:RabbitMq is empty; outbox messages will not be sent");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Lô đầy thì còn dòng chờ: quét tiếp ngay.
                if (await DispatchBatchAsync(stoppingToken) == BatchSize)
                    continue;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Outbox dispatch failed; retrying on next poll");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    // Trả về số thông điệp đã gửi. Gửi lỗi thì dòng ở lại và lần quét sau gửi tiếp, nên bên nhận có thể thấy bản trùng.
    private async Task<int> DispatchBatchAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OismDbContext>();
        // Outbox là bảng hạ tầng, quét mọi tenant: docs/architecture/multi-tenancy.md mục "Những chỗ được phép bỏ filter".
        var outbox = db.Set<OutboxMessage>().IgnoreQueryFilters();
        var table = db.Model.FindEntityType(typeof(OutboxMessage))!.GetSchemaQualifiedTableName();
        var lockBatch = $"SELECT * FROM {table} WHERE processed_at IS NULL ORDER BY occurred_at LIMIT {BatchSize} FOR UPDATE SKIP LOCKED";

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var batch = await db.Set<OutboxMessage>().FromSqlRaw(lockBatch).IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        if (batch.Count == 0)
            return 0;

        var sent = new List<Guid>(batch.Count);
        try
        {
            var channel = await OpenChannelAsync(ct);
            foreach (var message in batch)
            {
                // Channel bật publisher confirm: lời gọi chỉ xong khi broker đã nhận.
                await channel.BasicPublishAsync(
                    RabbitMq.Exchange, routingKey: message.Type, mandatory: false,
                    new BasicProperties { Persistent = true, MessageId = message.Id.ToString(), Type = message.Type },
                    Envelope(message), ct);
                sent.Add(message.Id);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var failed = batch[sent.Count];
            logger.LogWarning(exception, "Could not publish outbox message {EventId} of type {Type}", failed.Id, failed.Type);
            await outbox.Where(message => message.Id == failed.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(message => message.Attempts, message => message.Attempts + 1), ct);
        }

        var now = time.GetUtcNow();
        await outbox.Where(message => sent.Contains(message.Id))
            .ExecuteUpdateAsync(update => update.SetProperty(message => message.ProcessedAt, now), ct);
        await transaction.CommitAsync(ct);
        return sent.Count;
    }

    private static byte[] Envelope(OutboxMessage message) => JsonSerializer.SerializeToUtf8Bytes(
        new EventEnvelope(
            message.Id, message.Type, message.TenantId, message.OccurredAt,
            JsonSerializer.Deserialize<JsonElement>(message.Payload)),
        EventEnvelope.JsonOptions);

    private async Task<IChannel> OpenChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true })
            return _channel;

        await CloseAsync();
        _connection = await RabbitMq.ConnectAsync(configuration, automaticRecovery: false, ct);
        _channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
        await RabbitMq.DeclareExchangeAsync(_channel, ct);
        return _channel;
    }

    private async Task CloseAsync()
    {
        if (_channel is not null)
            await _channel.DisposeAsync();
        if (_connection is not null)
            await _connection.DisposeAsync();
        _channel = null;
        _connection = null;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await CloseAsync();
    }
}
