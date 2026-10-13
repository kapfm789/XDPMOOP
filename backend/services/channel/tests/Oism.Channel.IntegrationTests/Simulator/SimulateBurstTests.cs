using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Oism.BuildingBlocks.Auth;
using Oism.Channel.Domain;
using Oism.Contracts;

namespace Oism.Channel.IntegrationTests.Simulator;

// W3-03: công cụ bắn tải tự sinh webhook và gửi song song vào chính endpoint webhook (UC-SIM-01 AC-5).
public sealed class SimulateBurstTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private HttpClient Owner => factory.CreateClient(Roles.Owner, _tenantId);

    // Điều kiện xong của W3-03: bắn được 50 đơn đồng thời. Đây là công cụ để chạy T01 trên gói Compose.
    [Fact]
    [Trait("UseCase", "UC-SIM-01 AC-5")]
    public async Task SimulateBurst_FiftyOrdersAtOnceForOneSku_SendsFiftySignedWebhooksEachWithItsOwnEventAndOrder()
    {
        var branch = Guid.NewGuid();
        var shop = await Owner.CreateShopAsync("Shopee", branchId: branch);

        var response = await Owner.PostAsJsonAsync("/simulator/burst", new
        {
            channel = "shopee", shopId = shop.ShopId, skuCode = "AO-A-DEN-M", quantityPerOrder = 1, orders = 50, concurrency = 50,
            unitPrice = 150_000,
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(50, result.GetProperty("sent").GetInt32());
        Assert.NotEqual(Guid.Empty, result.GetProperty("burstId").GetGuid());
        // Mỗi webhook đi qua đúng đường của sàn: kiểm chữ ký, lưu bản ghi, ghi command.
        var webhooks = await factory.WebhooksAsync(_tenantId);
        Assert.Equal(50, webhooks.Count);
        Assert.All(webhooks, webhook => Assert.Equal((SalesChannel.Shopee, WebhookStatus.Submitted), (webhook.Channel, webhook.Status)));
        Assert.Equal(50, webhooks.Select(webhook => webhook.EventId).Distinct().Count());
        Assert.Equal(50, webhooks.Select(webhook => webhook.ExternalOrderId).Distinct().Count());
        var commands = await factory.OutboxAsync<SubmitOrder>(_tenantId);
        Assert.Equal(50, commands.Count);
        Assert.All(commands, command =>
        {
            Assert.Equal(("Shopee", branch), (command.Channel, command.BranchId));
            Assert.Equal(new SubmitOrderLine("AO-A-DEN-M", 1, 150_000, 0), Assert.Single(command.Lines));
        });
        Assert.Equal(
            webhooks.Select(webhook => webhook.ExternalOrderId).Order(),
            commands.Select(command => command.ExternalOrderId).Order());
    }

    [Theory]
    [InlineData("TikTok")]
    [InlineData("Lazada")]
    [Trait("UseCase", "UC-SIM-01 AC-5")]
    [Trait("UseCase", "UC-SIM-01 AC-7")]
    public async Task SimulateBurst_OtherChannels_BuildPayloadsTheirAdapterReadsBack(string channel)
    {
        var shop = await Owner.CreateShopAsync(channel);

        var response = await Owner.PostAsJsonAsync("/simulator/burst", new
        {
            channel, shopId = shop.ShopId, skuCode = "QUAN-01", quantityPerOrder = 2, orders = 3, concurrency = 2,
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(3, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sent").GetInt32());
        var commands = await factory.OutboxAsync<SubmitOrder>(_tenantId);
        Assert.Equal(3, commands.Count);
        // Không gửi unitPrice thì đơn giá là 0: `channel` không biết giá của SKU.
        Assert.All(commands, command => Assert.Equal((channel, new SubmitOrderLine("QUAN-01", 2, 0, 0)), (command.Channel, Assert.Single(command.Lines))));
        Assert.All(await factory.WebhooksAsync(_tenantId), webhook => Assert.Equal(WebhookStatus.Submitted, webhook.Status));
    }

    [Fact]
    [Trait("Scenario", "T14")]
    public async Task SimulateBurst_BadInputAnotherTenantsShopOrNotOwner_IsRefusedAndSendsNothing()
    {
        var shop = await Owner.CreateShopAsync();
        var otherTenant = Guid.NewGuid();
        var otherShop = await factory.CreateClient(Roles.Owner, otherTenant).CreateShopAsync();
        object Burst(string shopId, int orders = 1, int concurrency = 1, int quantityPerOrder = 1, string channel = "Shopee") =>
            new { channel, shopId, skuCode = "AO-01", quantityPerOrder, orders, concurrency };

        await (await Owner.PostAsJsonAsync("/simulator/burst", Burst(shop.ShopId, orders: 0))).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await Owner.PostAsJsonAsync("/simulator/burst", Burst(shop.ShopId, orders: 1001))).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await Owner.PostAsJsonAsync("/simulator/burst", Burst(shop.ShopId, concurrency: 0))).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await Owner.PostAsJsonAsync("/simulator/burst", Burst(shop.ShopId, quantityPerOrder: 0))).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await Owner.PostAsJsonAsync("/simulator/burst", Burst(shop.ShopId, channel: "Zalo"))).AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        // Shop của tenant khác, hoặc shop của kênh khác: không có trong tenant này.
        await (await Owner.PostAsJsonAsync("/simulator/burst", Burst(otherShop.ShopId))).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        await (await Owner.PostAsJsonAsync("/simulator/burst", Burst(shop.ShopId, channel: "Lazada"))).AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await factory.CreateClient(Roles.Staff, _tenantId).PostAsJsonAsync("/simulator/burst", Burst(shop.ShopId))).StatusCode);

        Assert.Empty(await factory.WebhooksAsync(_tenantId));
        Assert.Empty(await factory.WebhooksAsync(otherTenant));
    }
}
