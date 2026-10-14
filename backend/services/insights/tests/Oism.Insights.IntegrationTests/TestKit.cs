using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Messaging;
using Oism.BuildingBlocks.Tenancy;
using Oism.Contracts;
using Oism.Insights.Application.Projections.ApplyStockChange;
using Oism.Insights.Application.Projections.RecordSale;
using Oism.Insights.Application.Projections.UpsertSkuName;
using Oism.Insights.Infrastructure;
using RabbitMQ.Client;

namespace Oism.Insights.IntegrationTests;

internal static class TestKit
{
    // Mỗi lần gọi là một scope, tức một DbContext mới mang tenant của test, như một request.
    public static async Task<T> InTenantAsync<T>(this ApiFactory factory, Guid tenantId, Func<IServiceProvider, Task<T>> run)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
        return await run(scope.ServiceProvider);
    }

    public static Task<T> QueryAsync<T>(this ApiFactory factory, Guid tenantId, Func<InsightsDbContext, Task<T>> query) =>
        factory.InTenantAsync(tenantId, services => query(services.GetRequiredService<InsightsDbContext>()));

    // Như consumer OrderConfirmed đưa vào: cùng handler, không qua RabbitMQ. Consumer base lưu sau handler; ở đây cũng vậy.
    public static Task RecordSaleAsync(this ApiFactory factory, Guid tenantId, OrderConfirmed message) =>
        factory.InTenantAsync(tenantId, async services =>
        {
            await services.GetRequiredService<RecordSaleHandler>().Handle(message, default);
            return await services.GetRequiredService<InsightsDbContext>().SaveChangesAsync();
        });

    public static Task ApplyStockChangeAsync(this ApiFactory factory, Guid tenantId, StockChanged message) =>
        factory.InTenantAsync(tenantId, async services =>
        {
            await services.GetRequiredService<ApplyStockChangeHandler>().Handle(message, default);
            return await services.GetRequiredService<InsightsDbContext>().SaveChangesAsync();
        });

    public static Task UpsertSkuNameAsync(this ApiFactory factory, Guid tenantId, Guid skuId, string skuCode, string name) =>
        factory.InTenantAsync(tenantId, async services =>
        {
            await services.GetRequiredService<UpsertSkuNameHandler>().Handle(
                new SkuUpserted(skuId, Guid.NewGuid(), skuCode, name, [], 0, 0, IsActive: true, Version: 1), default);
            return await services.GetRequiredService<InsightsDbContext>().SaveChangesAsync();
        });

    // Mốc thời gian theo giờ Việt Nam, đổi về UTC như dữ liệu của event.
    public static DateTimeOffset Vn(int year, int month, int day, int hour = 10, int minute = 0) =>
        new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.FromHours(7)).ToUniversalTime();

    public static OrderConfirmed Confirmed(
        Guid branchId, DateTimeOffset confirmedAt, string channel = "Shopee", params OrderConfirmedLine[] lines) =>
        new(Guid.NewGuid(), channel, branchId, confirmedAt, lines);

    public static OrderConfirmedLine Line(
        Guid skuId, int quantity = 2, decimal unitPrice = 150_000, decimal discount = 0, decimal costPrice = 110_000) =>
        new(Guid.NewGuid(), skuId, quantity, unitPrice, discount, costPrice);
}

// Đưa thông điệp thẳng lên exchange như tiến trình đẩy outbox của `core` và `catalog` vẫn làm
// (docs/architecture/messaging.md), rồi chờ consumer thật của `insights` xử lý.
public sealed class EventBus(ApiFactory factory) : IAsyncDisposable
{
    private const string Exchange = "oism.events";

    private IConnection _connection = null!;
    private IChannel _channel = null!;

    // Khai báo trước queue bền đúng tên và đúng tham số như service, để thông điệp gửi trước khi consumer kịp nghe
    // không bị bỏ.
    public async Task StartAsync(params string[] types)
    {
        // Khởi động service: chạy migration và consumer.
        factory.CreateClient().Dispose();

        _connection = await new ConnectionFactory { Uri = new Uri(factory.Broker.GetConnectionString()) }.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();
        await _channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true);
        foreach (var type in types)
        {
            var queue = $"{InsightsDbContext.Schema}.{type}";
            await _channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false);
            await _channel.QueueBindAsync(queue, Exchange, routingKey: type);
        }
    }

    // Phong bì chung, routing key là tên thông điệp. Trả eventId đã dùng.
    public async Task<Guid> PublishAsync<TPayload>(TPayload payload, Guid tenantId, Guid? eventId = null)
    {
        var envelope = new EventEnvelope(
            eventId ?? Guid.NewGuid(), typeof(TPayload).Name, tenantId, DateTimeOffset.UtcNow,
            JsonSerializer.SerializeToElement(payload, EventEnvelope.JsonOptions));
        await _channel.BasicPublishAsync(
            Exchange, routingKey: typeof(TPayload).Name, mandatory: false, new BasicProperties { Persistent = true },
            JsonSerializer.SerializeToUtf8Bytes(envelope, EventEnvelope.JsonOptions));
        return envelope.EventId;
    }

    // Một queue xử lý lần lượt từng thông điệp: khi thông điệp chốt gửi sau cùng đã vào inbox thì mọi thông điệp
    // trước nó trên cùng queue đã được xử lý. Thông điệp chốt thuộc một tenant riêng nên không lẫn vào số liệu của test.
    public async Task DrainAsync<TPayload>(TPayload marker)
    {
        var markerId = await PublishAsync(marker, tenantId: Guid.NewGuid());

        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (!await factory.QueryAsync(Guid.Empty, db => db.Set<InboxMessage>().AnyAsync(message => message.EventId == markerId)))
        {
            Assert.True(DateTime.UtcNow < deadline, $"consumer never handled the {typeof(TPayload).Name} marker");
            await Task.Delay(100);
        }

        // Bản trùng và bản cũ được bỏ qua, không bị coi là lỗi.
        var errors = await _channel.QueueDeclarePassiveAsync($"{InsightsDbContext.Schema}.{typeof(TPayload).Name}.error");
        Assert.Equal(0u, errors.MessageCount);
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
