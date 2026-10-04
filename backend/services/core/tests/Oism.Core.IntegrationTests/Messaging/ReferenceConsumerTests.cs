using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Messaging;
using Oism.BuildingBlocks.Tenancy;
using Oism.Contracts;
using Oism.Core.Domain.References;
using Oism.Core.Infrastructure;
using RabbitMQ.Client;

namespace Oism.Core.IntegrationTests.Messaging;

// W1-08: consumer SkuUpserted và BranchUpserted dựng bản sao ở `core`. Thông điệp được đưa thẳng lên exchange
// như tiến trình đẩy outbox của `catalog` và `identity` vẫn làm (docs/architecture/messaging.md).
public sealed class ReferenceConsumerTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private const string Exchange = "oism.events";

    private readonly Guid _tenantId = Guid.NewGuid();
    private IConnection _connection = null!;
    private IChannel _channel = null!;

    public async Task InitializeAsync()
    {
        // Khởi động service: chạy migration và consumer.
        factory.CreateClient().Dispose();

        _connection = await new ConnectionFactory { Uri = new Uri(factory.Broker.GetConnectionString()) }.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        // Khai báo queue bền đúng tên và đúng tham số như service, để thông điệp gửi trước khi consumer kịp nghe không bị bỏ.
        await _channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true);
        foreach (var type in new[] { nameof(SkuUpserted), nameof(BranchUpserted) })
        {
            var queue = $"{CoreDbContext.Schema}.{type}";
            await _channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false);
            await _channel.QueueBindAsync(queue, Exchange, routingKey: type);
        }
    }

    public async Task DisposeAsync()
    {
        await _channel.DisposeAsync();
        await _connection.DisposeAsync();
    }

    // Điều kiện xong của W1-08: gửi lại cùng event không tạo bản ghi trùng.
    [Fact]
    [Trait("UseCase", "UC-PROD-02 AC-4")]
    public async Task SkuUpserted_SameEventDeliveredTwice_CreatesOneSkuRefAndOneInboxRow()
    {
        var sku = Sku("AO-01", version: 1);
        var eventId = Guid.NewGuid();

        await PublishAsync(sku, eventId);
        await PublishAsync(sku, eventId);
        await DrainAsync(nameof(SkuUpserted));

        var copy = Assert.Single(await SkuRefsAsync(_tenantId));
        Assert.Equal(_tenantId, copy.TenantId);
        Assert.Equal((sku.SkuId, "AO-01", "Áo thun, Đen", 150_000m, 120_000m, true, 1L),
            (copy.SkuId, copy.SkuCode, copy.Name, copy.RetailPrice, copy.WholesalePrice, copy.IsActive, copy.Version));
        Assert.Equal(["2000000000015", "ABC123"], copy.Barcodes);
        Assert.Equal(1, await QueryAsync(tenantId: null, db => db.Set<InboxMessage>().CountAsync(message => message.EventId == eventId)));
    }

    // Thông điệp trạng thái tới sai thứ tự: bản có version nhỏ hơn hoặc bằng bản đang giữ bị bỏ qua.
    [Fact]
    [Trait("UseCase", "UC-PROD-02 AC-4")]
    [Trait("UseCase", "UC-PROD-04 AC-5")]
    public async Task SkuUpserted_NewerVersionOverwrites_OlderOrEqualVersionIsIgnored()
    {
        var created = Sku("AO-01", version: 1);
        var repriced = created with { RetailPrice = 160_000, WholesalePrice = 130_000, Barcodes = [], Version = 3 };
        var stale = created with { Name = "Bản cũ tới muộn", Version = 2 };
        var sameVersion = repriced with { Name = "Cùng version, khác nội dung" };

        await PublishAsync(created);
        await PublishAsync(repriced);
        await PublishAsync(stale);
        await PublishAsync(sameVersion);
        await DrainAsync(nameof(SkuUpserted));

        var copy = Assert.Single(await SkuRefsAsync(_tenantId));
        Assert.Equal(("Áo thun, Đen", 160_000m, 130_000m, 3L), (copy.Name, copy.RetailPrice, copy.WholesalePrice, copy.Version));
        Assert.Empty(copy.Barcodes);

        // UC-PROD-02 AC-5: SKU ngừng bán vẫn còn bản sao, chỉ đổi cờ.
        await PublishAsync(repriced with { IsActive = false, Version = 4 });
        await DrainAsync(nameof(SkuUpserted));

        copy = Assert.Single(await SkuRefsAsync(_tenantId));
        Assert.Equal((false, 4L), (copy.IsActive, copy.Version));
    }

    // Điều kiện xong của W1-05, phía nhận: `core` có BranchRef sau một nhịp event; tắt chi nhánh thì bản sao tắt theo.
    [Fact]
    [Trait("UseCase", "UC-AUTH-04 AC-3")]
    [Trait("UseCase", "UC-AUTH-04 AC-4")]
    public async Task BranchUpserted_CreatedThenDeactivated_BranchRefFollowsAndDuplicatesChangeNothing()
    {
        var branchId = Guid.NewGuid();
        var created = new BranchUpserted(branchId, "CH-01", "Cửa hàng 1", "Store", IsActive: true, Version: 1);
        var deactivated = created with { Name = "Cửa hàng một", Type = "Warehouse", IsActive = false, Version = 2 };
        var eventId = Guid.NewGuid();

        await PublishAsync(created, eventId);
        await PublishAsync(created, eventId);
        await DrainAsync(nameof(BranchUpserted));

        var copy = Assert.Single(await BranchRefsAsync(_tenantId));
        Assert.Equal((_tenantId, branchId, "CH-01", "Cửa hàng 1", "Store", true, 1L),
            (copy.TenantId, copy.BranchId, copy.Code, copy.Name, copy.Type, copy.IsActive, copy.Version));

        await PublishAsync(deactivated);
        // Bản tạo tới lại sau bản tắt, với eventId mới: version nhỏ hơn nên bị bỏ qua.
        await PublishAsync(created);
        await DrainAsync(nameof(BranchUpserted));

        copy = Assert.Single(await BranchRefsAsync(_tenantId));
        Assert.Equal(("CH-01", "Cửa hàng một", "Warehouse", false, 2L), (copy.Code, copy.Name, copy.Type, copy.IsActive, copy.Version));
    }

    // NFR-TENANT-01: consumer đặt tenant context theo thông điệp; bản sao chỉ thuộc tenant đó.
    [Fact]
    [Trait("Scenario", "T14")]
    public async Task Upserted_FromTwoTenants_EachTenantSeesOnlyItsOwnCopies()
    {
        var otherTenantId = Guid.NewGuid();
        var skuOfThis = Sku("AO-01", version: 1);
        // Cùng mã SKU và cùng mã chi nhánh ở hai tenant là hợp lệ.
        var skuOfOther = Sku("AO-01", version: 1) with { Name = "Của tenant khác" };
        var branchOfOther = new BranchUpserted(Guid.NewGuid(), "CH-01", "Của tenant khác", "Store", IsActive: true, Version: 1);

        await PublishAsync(skuOfThis);
        await PublishAsync(skuOfOther, tenantId: otherTenantId);
        await PublishAsync(branchOfOther, tenantId: otherTenantId);
        await DrainAsync(nameof(SkuUpserted));
        await DrainAsync(nameof(BranchUpserted));

        Assert.Equal(skuOfThis.SkuId, Assert.Single(await SkuRefsAsync(_tenantId)).SkuId);
        Assert.Empty(await BranchRefsAsync(_tenantId));
        var copyOfOther = Assert.Single(await SkuRefsAsync(otherTenantId));
        Assert.Equal((skuOfOther.SkuId, otherTenantId, "Của tenant khác"), (copyOfOther.SkuId, copyOfOther.TenantId, copyOfOther.Name));
        Assert.Equal(branchOfOther.BranchId, Assert.Single(await BranchRefsAsync(otherTenantId)).BranchId);
    }

    private static SkuUpserted Sku(string code, long version) => new(
        Guid.NewGuid(), Guid.NewGuid(), code, "Áo thun, Đen", ["2000000000015", "ABC123"], 150_000m, 120_000m, IsActive: true, version);

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
    // khi thông điệp chốt (gửi sau cùng, thuộc một tenant riêng) đã có bản sao thì mọi thông điệp trước nó đã được xử lý.
    private async Task DrainAsync(string type)
    {
        var (markerTenantId, markerId) = (Guid.NewGuid(), Guid.NewGuid());
        if (type == nameof(SkuUpserted))
            await PublishAsync(Sku("MARKER", version: 1) with { SkuId = markerId }, tenantId: markerTenantId);
        else
            await PublishAsync(new BranchUpserted(markerId, "MARKER", "Chốt", "Store", IsActive: true, Version: 1), tenantId: markerTenantId);

        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (!await QueryAsync(markerTenantId, async db =>
            await db.SkuRefs.AnyAsync(sku => sku.SkuId == markerId) || await db.BranchRefs.AnyAsync(branch => branch.BranchId == markerId)))
        {
            Assert.True(DateTime.UtcNow < deadline, $"consumer never handled the {type} marker");
            await Task.Delay(100);
        }

        // Bản trùng và bản cũ được bỏ qua, không bị coi là lỗi.
        Assert.Equal(0u, (await _channel.QueueDeclarePassiveAsync($"{CoreDbContext.Schema}.{type}.error")).MessageCount);
    }

    private Task<List<SkuRef>> SkuRefsAsync(Guid tenantId) =>
        QueryAsync(tenantId, db => db.SkuRefs.AsNoTracking().ToListAsync());

    private Task<List<BranchRef>> BranchRefsAsync(Guid tenantId) =>
        QueryAsync(tenantId, db => db.BranchRefs.AsNoTracking().ToListAsync());

    private async Task<T> QueryAsync<T>(Guid? tenantId, Func<CoreDbContext, Task<T>> query)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        if (tenantId is { } id)
            scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(id);
        return await query(scope.ServiceProvider.GetRequiredService<CoreDbContext>());
    }
}
