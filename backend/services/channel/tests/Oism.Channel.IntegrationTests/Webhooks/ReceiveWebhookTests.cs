using System.Net;
using System.Net.Http.Json;
using Oism.BuildingBlocks.Auth;
using Oism.BuildingBlocks.Messaging;
using Oism.Channel.Domain;
using Oism.Contracts;
using static Oism.Channel.IntegrationTests.TestKit;

namespace Oism.Channel.IntegrationTests.Webhooks;

// W3-03: nhận webhook ba sàn, kiểm chữ ký, chống trùng, chuẩn hóa rồi ghi SubmitOrder vào outbox (UC-SIM-01; FR-SIM-01, FR-ORD-01).
public sealed class ReceiveWebhookTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private HttpClient Owner => factory.CreateClient(Roles.Owner, _tenantId);

    [Theory]
    [InlineData("shopee", "Shopee")]
    [InlineData("tiktok", "TikTok")]
    [InlineData("lazada", "Lazada")]
    [Trait("UseCase", "UC-SIM-01 AC-1")]
    [Trait("UseCase", "UC-SIM-01 AC-7")]
    [Trait("UseCase", "UC-ORD-01 AC-7")]
    public async Task ReceiveWebhook_ConfiguredShopAndValidSignature_Returns202StoresTheEventAndQueuesTheSameCanonicalOrder(
        string route, string channel)
    {
        var branch = Guid.NewGuid();
        var shop = await Owner.CreateShopAsync(channel, branchId: branch);
        var (eventId, orderId) = ($"evt-{Guid.NewGuid():N}", $"ORD-{Guid.NewGuid():N}");
        var body = channel switch
        {
            "Shopee" => Shopee(eventId, shop.ShopId, orderId),
            "TikTok" => TikTok(eventId, shop.ShopId, orderId),
            _ => Lazada(eventId, shop.ShopId, orderId),
        };

        var status = await factory.SendWebhookAsync(route, body, shop.Secret!).AcceptedStatusAsync(eventId);

        Assert.Equal("Submitted", status);
        // Bản ghi thuộc tenant của shop, dù webhook không mang JWT.
        var webhook = Assert.Single(await factory.WebhooksAsync(_tenantId));
        Assert.Equal(
            (_tenantId, channel, eventId, orderId, WebhookStatus.Submitted, (string?)null, (Guid?)null),
            (webhook.TenantId, webhook.Channel.ToString(), webhook.EventId, webhook.ExternalOrderId, webhook.Status, webhook.Reason, webhook.OrderId));
        Assert.Contains(eventId, webhook.Payload);
        // Ba hình dạng payload khác nhau ra cùng một Canonical Order; chi nhánh lấy từ cấu hình shop.
        var command = Assert.Single(await factory.OutboxAsync<SubmitOrder>(_tenantId));
        Assert.Equal(
            (channel, orderId, branch, new DateTimeOffset(2026, 10, 20, 3, 15, 27, TimeSpan.Zero)),
            (command.Channel, command.ExternalOrderId, command.BranchId, command.PlacedAt));
        Assert.Equal(new SubmitOrderLine("AO-A-DEN-M", 1, 150_000, 0), Assert.Single(command.Lines));
        Assert.Equal(_tenantId, (await factory.QueryAsync(_tenantId, db => Task.FromResult(db.Set<OutboxMessage>().Single()))).TenantId);
    }

    [Fact]
    [Trait("UseCase", "UC-SIM-01 AC-2")]
    [Trait("UseCase", "UC-SIM-01 AC-3")]
    public async Task ReceiveWebhook_WrongSignatureUnknownShopOrUnreadableBody_IsRefusedAndNothingIsStored()
    {
        var shop = await Owner.CreateShopAsync();
        var disabled = await Owner.CreateShopAsync();
        await Owner.PatchAsJsonAsync($"/channel-shops/{disabled.Id}/active", new { isActive = false });
        var body = Shopee("evt-1", shop.ShopId, "ORD-1");

        // AC-2: chữ ký của body khác, chữ ký viết hoa, hoặc không có header.
        await (await factory.SendWebhookAsync("shopee", body, shop.Secret!, Sign(body + " ", shop.Secret!)))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, "invalid_signature");
        await (await factory.SendWebhookAsync("shopee", body, shop.Secret!, Sign(body, shop.Secret!).ToUpperInvariant()))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, "invalid_signature");
        await (await factory.SendWebhookAsync("shopee", body, shop.Secret!, signature: string.Empty))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, "invalid_signature");
        // AC-3: shop chưa cấu hình, shop của kênh khác, shop đã tắt, kênh không có.
        var unknown = Shopee("evt-2", "no-such-shop", "ORD-2");
        await (await factory.SendWebhookAsync("shopee", unknown, shop.Secret!)).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        var otherChannel = TikTok("evt-3", shop.ShopId, "ORD-3");
        await (await factory.SendWebhookAsync("tiktok", otherChannel, shop.Secret!)).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        var toDisabled = Shopee("evt-4", disabled.ShopId, "ORD-4");
        await (await factory.SendWebhookAsync("shopee", toDisabled, disabled.Secret!)).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await factory.SendWebhookAsync("zalo", body, shop.Secret!)).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        // Body không phải JSON, hoặc thiếu mã sự kiện: không biết webhook của ai, không chống trùng được.
        await (await factory.SendWebhookAsync("shopee", "not json", shop.Secret!)).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        var noEventId = $$"""{"shop_id":"{{shop.ShopId}}","ordersn":"ORD-5"}""";
        await (await factory.SendWebhookAsync("shopee", noEventId, shop.Secret!)).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");

        Assert.Empty(await factory.WebhooksAsync(_tenantId));
        Assert.Empty(await factory.OutboxAsync<SubmitOrder>(_tenantId));
    }

    [Fact]
    [Trait("Scenario", "T03")]
    [Trait("UseCase", "UC-SIM-01 AC-4")]
    public async Task ReceiveWebhook_SameEventSentAgainAndTenTimesAtOnce_Returns202EachTimeButStoresOneEventAndOneCommand()
    {
        var shop = await Owner.CreateShopAsync();
        var body = Shopee("evt-dup", shop.ShopId, "ORD-DUP");
        // Cùng mã sự kiện ở shop của tenant khác là một webhook khác: chỉ mục unique tính theo tenant.
        var otherTenant = Guid.NewGuid();
        var otherShop = await factory.CreateClient(Roles.Owner, otherTenant).CreateShopAsync();
        var otherBody = Shopee("evt-dup", otherShop.ShopId, "ORD-DUP");

        var first = await factory.SendWebhookAsync("shopee", body, shop.Secret!).AcceptedStatusAsync("evt-dup");
        var second = await factory.SendWebhookAsync("shopee", body, shop.Secret!).AcceptedStatusAsync("evt-dup");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var burst = Enumerable.Range(0, 10).Select(async _ =>
        {
            await gate.Task;
            return await factory.SendWebhookAsync("shopee", body, shop.Secret!).AcceptedStatusAsync("evt-dup");
        }).ToArray();
        // Mười bản đầu tiên của một webhook mới tới cùng lúc: chỉ mục unique chọn đúng một bản.
        var fresh = Shopee("evt-race", shop.ShopId, "ORD-RACE");
        var race = Enumerable.Range(0, 10).Select(async _ =>
        {
            await gate.Task;
            return await factory.SendWebhookAsync("shopee", fresh, shop.Secret!).AcceptedStatusAsync("evt-race");
        }).ToArray();
        gate.SetResult();
        var statuses = await Task.WhenAll(burst.Concat(race));
        var other = await factory.SendWebhookAsync("shopee", otherBody, otherShop.Secret!).AcceptedStatusAsync("evt-dup");

        Assert.All(statuses.Append(first).Append(second).Append(other), status => Assert.Equal("Submitted", status));
        Assert.Equal(["evt-dup", "evt-race"], (await factory.WebhooksAsync(_tenantId)).Select(webhook => webhook.EventId).Order());
        Assert.Equal(["ORD-DUP", "ORD-RACE"], (await factory.OutboxAsync<SubmitOrder>(_tenantId)).Select(command => command.ExternalOrderId).Order());
        Assert.Single(await factory.WebhooksAsync(otherTenant));
        Assert.Single(await factory.OutboxAsync<SubmitOrder>(otherTenant));
    }

    [Fact]
    [Trait("UseCase", "UC-SIM-01 AC-6")]
    public async Task ReceiveWebhook_PayloadThatCannotBecomeACanonicalOrder_Returns202StoresItAsFailedAndQueuesNoCommand()
    {
        var shop = await Owner.CreateShopAsync();
        var wrongType = $$"""{"event_id":"evt-type","shop_id":"{{shop.ShopId}}","ordersn":"ORD-1","create_time":1792466127,"item_list":[{"model_sku":"AO-01","quantity":"hai","price":150000,"discount":0}]}""";
        var noLines = $$"""{"event_id":"evt-empty","shop_id":"{{shop.ShopId}}","ordersn":"ORD-2","create_time":1792466127,"item_list":[]}""";
        var noOrderId = $$"""{"event_id":"evt-noid","shop_id":"{{shop.ShopId}}","create_time":1792466127,"item_list":[{"model_sku":"AO-01","quantity":1,"price":150000,"discount":0}]}""";
        var overDiscount = Shopee("evt-discount", shop.ShopId, "ORD-4").Replace("\"discount\":0", "\"discount\":150001");

        string[] statuses =
        [
            await factory.SendWebhookAsync("shopee", wrongType, shop.Secret!).AcceptedStatusAsync("evt-type"),
            await factory.SendWebhookAsync("shopee", noLines, shop.Secret!).AcceptedStatusAsync("evt-empty"),
            await factory.SendWebhookAsync("shopee", noOrderId, shop.Secret!).AcceptedStatusAsync("evt-noid"),
            await factory.SendWebhookAsync("shopee", overDiscount, shop.Secret!).AcceptedStatusAsync("evt-discount"),
            // Sàn gửi lại webhook xấu: vẫn 202 với trạng thái cũ, không thêm bản ghi.
            await factory.SendWebhookAsync("shopee", noLines, shop.Secret!).AcceptedStatusAsync("evt-empty"),
        ];

        Assert.All(statuses, status => Assert.Equal("Failed", status));
        var webhooks = await factory.WebhooksAsync(_tenantId);
        Assert.Equal(4, webhooks.Count);
        Assert.All(webhooks, webhook =>
        {
            Assert.Equal(WebhookStatus.Failed, webhook.Status);
            Assert.False(string.IsNullOrWhiteSpace(webhook.Reason));
            Assert.Null(webhook.ExternalOrderId);
        });
        Assert.Equal("Đơn không có dòng hàng", webhooks.Single(webhook => webhook.EventId == "evt-empty").Reason);
        Assert.Empty(await factory.OutboxAsync<SubmitOrder>(_tenantId));

        // AC-6: danh sách webhook lọc theo trạng thái và kênh, mới nhận nhất trước, kèm lý do.
        var good = await factory.SendWebhookAsync("shopee", Shopee("evt-good", shop.ShopId, "ORD-5"), shop.Secret!).AcceptedStatusAsync("evt-good");
        var failed = await Owner.GetFromJsonAsync<Listing>("/webhook-events?status=Failed&channel=shopee&pageSize=3");
        var submitted = await Owner.GetFromJsonAsync<Listing>("/webhook-events?status=Submitted");
        var ofOtherChannel = await Owner.GetFromJsonAsync<Listing>("/webhook-events?channel=Lazada");

        Assert.Equal("Submitted", good);
        Assert.Equal((4, 3, 1, 3), (failed!.Total, failed.Items.Count, failed.Page, failed.PageSize));
        Assert.All(failed.Items, item => Assert.Equal(("Shopee", "Failed"), (item.Channel, item.Status)));
        Assert.Equal("evt-discount", failed.Items[0].EventId);
        Assert.NotNull(failed.Items[0].Reason);
        Assert.Equal(("evt-good", "ORD-5", (string?)null), (Assert.Single(submitted!.Items).EventId, submitted.Items[0].ExternalOrderId, submitted.Items[0].Reason));
        Assert.Empty(ofOtherChannel!.Items);
        await (await Owner.GetAsync("/webhook-events?status=Lost")).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await Owner.GetAsync("/webhook-events?pageSize=101")).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        // T14: tenant khác không thấy webhook nào; Staff không vào được (docs/architecture/security.md).
        Assert.Empty((await factory.CreateClient(Roles.Owner, Guid.NewGuid()).GetFromJsonAsync<Listing>("/webhook-events"))!.Items);
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.CreateClient(Roles.Staff, _tenantId).GetAsync("/webhook-events")).StatusCode);
    }

    private sealed record Item(string Channel, string EventId, string? ExternalOrderId, string Status, string? Reason);

    private sealed record Listing(IReadOnlyList<Item> Items, int Page, int PageSize, int Total);
}
