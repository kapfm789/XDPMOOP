using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Oism.BuildingBlocks.Messaging;
using Oism.BuildingBlocks.Persistence;
using Oism.BuildingBlocks.Tenancy;
using Oism.Contracts;
using Oism.SharedKernel;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Oism.BuildingBlocks.IntegrationTests.Messaging;

// Thay đổi nghiệp vụ mẫu mà consumer tạo ra khi nhận SkuUpserted.
public sealed class SkuCopy(Guid skuId, string skuCode) : ITenantOwned
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid TenantId { get; private set; }

    public Guid SkuId { get; private set; } = skuId;

    public string SkuCode { get; private set; } = skuCode;
}

public sealed class MessagingDbContext(DbContextOptions<MessagingDbContext> options, ITenantContext tenant)
    : OismDbContext(options, tenant, Schema), IHasOutbox, IHasInbox
{
    public const string Schema = "msgtest";

    public DbSet<SkuCopy> SkuCopies => Set<SkuCopy>();
}

public sealed class SkuUpsertedConsumer(MessagingDbContext db) : EventConsumer<SkuUpserted>
{
    public const string PoisonCode = "POISON";

    public override Task HandleAsync(SkuUpserted payload, CancellationToken ct)
    {
        db.Add(new SkuCopy(payload.SkuId, payload.SkuCode));
        return payload.SkuCode == PoisonCode
            ? throw new InvalidOperationException("Cannot handle this message.")
            : Task.CompletedTask;
    }
}

public sealed class MessagingFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public RabbitMqContainer Broker { get; } = NewBroker();

    public IHost Host { get; private set; } = null!;

    public static RabbitMqContainer NewBroker() => new RabbitMqBuilder("rabbitmq:4-management-alpine").Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), Broker.StartAsync());
        Host = await StartHostAsync("main", Broker.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await Host.StopAsync();
        Host.Dispose();
        await Broker.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    // Một "service" có cả outbox lẫn consumer, dùng database riêng để các host không quét outbox của nhau.
    public async Task<IHost> StartHostAsync(string database, string rabbitMqUri)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString()) { Database = database }.ConnectionString;

        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:RabbitMq"] = rabbitMqUri;
        builder.Services.AddScoped<ITenantContext, TenantContext>();
        builder.Services.AddDbContext<MessagingDbContext>(options => options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());
        builder.Services.AddOismMessaging<MessagingDbContext>();
        builder.Services.AddEventConsumer<SkuUpsertedConsumer, SkuUpserted>();

        var host = builder.Build();
        await using (var scope = host.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<MessagingDbContext>().Database.EnsureCreatedAsync();
        await host.StartAsync();
        return host;
    }
}

// Điều kiện xong của W1-13 (NFR-SEC-02): event mẫu đi từ outbox tới consumer; gửi lại không xử lý trùng.
public sealed class MessagingTests(MessagingFixture fixture) : IClassFixture<MessagingFixture>
{
    private const string Queue = $"{MessagingDbContext.Schema}.{nameof(SkuUpserted)}";

    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public async Task Outbox_EventSavedInTenant_ReachesConsumerInSameTenant()
    {
        var sku = Sku("A-01");

        var eventId = await EnqueueAsync(fixture.Host, sku);

        await WaitForCopyAsync(fixture.Host, sku);
        await WaitForDispatchAsync(fixture.Host, eventId);
        var copy = Assert.Single(await CopiesAsync(fixture.Host, _tenantId, sku));
        Assert.Equal(_tenantId, copy.TenantId);
        Assert.Equal("A-01", copy.SkuCode);
        Assert.Empty(await CopiesAsync(fixture.Host, Guid.NewGuid(), sku));
        Assert.True(await InInboxAsync(fixture.Host, eventId));
    }

    [Fact]
    public async Task Consumer_SameEventDeliveredAgain_HandlesItOnce()
    {
        var sku = Sku("B-01");
        var eventId = await EnqueueAsync(fixture.Host, sku);
        await WaitForCopyAsync(fixture.Host, sku);
        await WaitForDispatchAsync(fixture.Host, eventId);
        var errorsBefore = await ErrorCountAsync();

        // Đặt lại dòng outbox như thể lần gửi trước chưa kịp đánh dấu: tiến trình đẩy gửi lại đúng thông điệp đó.
        await QueryAsync(fixture.Host, tenantId: null, db => db.Set<OutboxMessage>().IgnoreQueryFilters()
            .Where(message => message.Id == eventId)
            .ExecuteUpdateAsync(update => update.SetProperty(message => message.ProcessedAt, (DateTimeOffset?)null)));
        await WaitForDispatchAsync(fixture.Host, eventId);

        // Consumer xử lý tuần tự: khi thông điệp chốt này xong thì bản gửi lại đã đi qua consumer.
        var marker = Sku("B-02");
        await EnqueueAsync(fixture.Host, marker);
        await WaitForCopyAsync(fixture.Host, marker);
        Assert.Single(await CopiesAsync(fixture.Host, _tenantId, sku));
        // Bản trùng được bỏ qua, không bị coi là lỗi.
        Assert.Equal(errorsBefore, await ErrorCountAsync());
    }

    [Fact]
    public async Task Consumer_HandlerKeepsFailing_MovesMessageToErrorQueueAndHandlesNextOne()
    {
        var poison = Sku(SkuUpsertedConsumer.PoisonCode);
        var poisonId = await EnqueueAsync(fixture.Host, poison);
        var next = Sku("C-01");
        await EnqueueAsync(fixture.Host, next);

        await WaitForCopyAsync(fixture.Host, next);

        // Lỗi thì rollback cả dòng inbox lẫn thay đổi nghiệp vụ.
        Assert.Empty(await CopiesAsync(fixture.Host, _tenantId, poison));
        Assert.False(await InInboxAsync(fixture.Host, poisonId));

        await using var connection = await ConnectAsync(fixture.Broker);
        await using var channel = await connection.CreateChannelAsync();
        var failed = await channel.BasicGetAsync($"{Queue}.error", autoAck: true);
        Assert.NotNull(failed);
        var envelope = JsonSerializer.Deserialize<EventEnvelope>(failed.Body.Span, EventEnvelope.JsonOptions)!;
        Assert.Equal(poisonId, envelope.EventId);
        Assert.Equal(nameof(SkuUpserted), envelope.Type);
        Assert.Equal(_tenantId, envelope.TenantId);
        Assert.Equal(SkuUpsertedConsumer.PoisonCode, envelope.Payload.GetProperty("skuCode").GetString());
    }

    [Fact]
    public async Task Outbox_BrokerDownAtCommit_CommitsAndSendsWhenBrokerIsBack()
    {
        // Không có broker nào nghe ở cổng 1.
        var host = await fixture.StartHostAsync("broker_down", "amqp://guest:guest@localhost:1");
        await using var broker = MessagingFixture.NewBroker();
        try
        {
            var sku = Sku("D-01");

            var eventId = await EnqueueAsync(host, sku);

            await WaitUntilAsync(async () => (await OutboxAsync(host, eventId)).Attempts > 0, "dispatcher never tried to send");
            Assert.Null((await OutboxAsync(host, eventId)).ProcessedAt);

            // Broker chạy lại với queue bền đã có từ trước, theo tên ở docs/architecture/messaging.md.
            await broker.StartAsync();
            await using (var connection = await ConnectAsync(broker))
            await using (var channel = await connection.CreateChannelAsync())
            {
                await channel.ExchangeDeclareAsync("oism.events", ExchangeType.Topic, durable: true);
                await channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false);
                await channel.QueueBindAsync(Queue, "oism.events", nameof(SkuUpserted));
            }

            host.Services.GetRequiredService<IConfiguration>()["ConnectionStrings:RabbitMq"] = broker.GetConnectionString();

            await WaitForCopyAsync(host, sku);
            await WaitForDispatchAsync(host, eventId);
            Assert.Single(await CopiesAsync(host, _tenantId, sku));
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    private static SkuUpserted Sku(string code) =>
        new(Guid.NewGuid(), Guid.NewGuid(), code, $"Sản phẩm {code}", ["2000000000015"], 150_000m, 120_000m, IsActive: true, Version: 1);

    // Ghi event vào outbox như một use case: trong tenant context, cùng SaveChanges với thay đổi nghiệp vụ.
    private async Task<Guid> EnqueueAsync(IHost host, SkuUpserted sku)
    {
        var message = OutboxMessage.Create(sku, DateTimeOffset.UtcNow);
        await QueryAsync(host, _tenantId, db =>
        {
            db.Add(message);
            return db.SaveChangesAsync();
        });
        return message.Id;
    }

    private Task WaitForCopyAsync(IHost host, SkuUpserted sku) =>
        WaitUntilAsync(async () => (await CopiesAsync(host, _tenantId, sku)).Count > 0, $"consumer never handled {sku.SkuCode}");

    private static Task WaitForDispatchAsync(IHost host, Guid eventId) =>
        WaitUntilAsync(async () => (await OutboxAsync(host, eventId)).ProcessedAt is not null, "outbox message was never marked as sent");

    private static Task<List<SkuCopy>> CopiesAsync(IHost host, Guid tenantId, SkuUpserted sku) =>
        QueryAsync(host, tenantId, db => db.SkuCopies.Where(copy => copy.SkuId == sku.SkuId).ToListAsync());

    private static Task<OutboxMessage> OutboxAsync(IHost host, Guid eventId) =>
        QueryAsync(host, tenantId: null, db => db.Set<OutboxMessage>().IgnoreQueryFilters().AsNoTracking().SingleAsync(message => message.Id == eventId));

    private static Task<bool> InInboxAsync(IHost host, Guid eventId) =>
        QueryAsync(host, tenantId: null, db => db.Set<InboxMessage>().AnyAsync(message => message.EventId == eventId));

    private static async Task<T> QueryAsync<T>(IHost host, Guid? tenantId, Func<MessagingDbContext, Task<T>> query)
    {
        await using var scope = host.Services.CreateAsyncScope();
        if (tenantId is { } id)
            scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(id);
        return await query(scope.ServiceProvider.GetRequiredService<MessagingDbContext>());
    }

    private async Task<uint> ErrorCountAsync()
    {
        await using var connection = await ConnectAsync(fixture.Broker);
        await using var channel = await connection.CreateChannelAsync();
        return (await channel.QueueDeclarePassiveAsync($"{Queue}.error")).MessageCount;
    }

    private static Task<IConnection> ConnectAsync(RabbitMqContainer broker) =>
        new ConnectionFactory { Uri = new Uri(broker.GetConnectionString()) }.CreateConnectionAsync();

    // Tiến trình đẩy outbox và consumer chạy nền thật, nên test chờ kết quả thay vì điều khiển đồng hồ.
    private static async Task WaitUntilAsync(Func<Task<bool>> condition, string failure)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!await condition())
        {
            Assert.True(DateTime.UtcNow < deadline, failure);
            await Task.Delay(100);
        }
    }
}
