using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Oism.BuildingBlocks.Auth;
using Oism.Contracts;
using static Oism.Channel.IntegrationTests.TestKit;

namespace Oism.Channel.IntegrationTests.Tenancy;

// W5-02, T14 cho channel. Webhook không mang JWT: tenant lấy từ shop nhận webhook (docs/architecture/multi-tenancy.md),
// nên webhook gửi vào shop của B chỉ để lại dấu vết ở B. Tắt shop, nhật ký webhook và bắn tải có test ở Shops và Simulator.
public sealed class TenantIsolationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    [Trait("Scenario", "T14")]
    public async Task Webhook_ToTheShopOfAnotherTenant_IsRecordedAndQueuedOnlyInThatTenant()
    {
        var (tenantA, tenantB) = (Guid.NewGuid(), Guid.NewGuid());
        var (ownerA, ownerB) = (factory.CreateClient(Roles.Owner, tenantA), factory.CreateClient(Roles.Owner, tenantB));
        var shopOfA = await ownerA.CreateShopAsync("Shopee");
        var shopOfB = await ownerB.CreateShopAsync("Shopee");

        await factory.SendWebhookAsync("shopee", Shopee("evt-b", shopOfB.ShopId, "ORD-1"), shopOfB.Secret!).AcceptedStatusAsync("evt-b");
        // Webhook ghi mã shop của B nhưng ký bằng secret của A: chữ ký sai, không lưu gì ở đâu cả.
        var forged = await factory.SendWebhookAsync("shopee", Shopee("evt-forged", shopOfB.ShopId, "ORD-2"), shopOfA.Secret!);

        Assert.Equal(HttpStatusCode.Unauthorized, forged.StatusCode);
        Assert.Equal(0, (await ownerA.GetFromJsonAsync<JsonElement>("/webhook-events"))!.GetProperty("total").GetInt32());
        Assert.Empty(await factory.WebhooksAsync(tenantA));
        Assert.Empty(await factory.OutboxAsync<SubmitOrder>(tenantA));

        var listedForB = (await ownerB.GetFromJsonAsync<JsonElement>("/webhook-events"))!.GetProperty("items");
        Assert.Equal(["evt-b"], listedForB.EnumerateArray().Select(item => item.GetProperty("eventId").GetString()));
        Assert.Equal("evt-b", Assert.Single(await factory.WebhooksAsync(tenantB)).EventId);
        Assert.Equal(("ORD-1", shopOfB.BranchId), CommandOf(Assert.Single(await factory.OutboxAsync<SubmitOrder>(tenantB))));
    }

    private static (string, Guid) CommandOf(SubmitOrder command) => (command.ExternalOrderId, command.BranchId);
}
