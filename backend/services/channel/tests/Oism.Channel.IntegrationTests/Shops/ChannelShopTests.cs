using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Oism.BuildingBlocks.Auth;
using Oism.Channel.Application.Shops;
using static Oism.Channel.IntegrationTests.TestKit;

namespace Oism.Channel.IntegrationTests.Shops;

// W3-03: cấu hình shop của sàn, ánh xạ shop sang tenant và chi nhánh (UC-SIM-01; docs/design/api/channel.md).
public sealed class ChannelShopTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private HttpClient Owner => factory.CreateClient(Roles.Owner, _tenantId);

    [Fact]
    [Trait("UseCase", "UC-SIM-01 AC-1")]
    public async Task ChannelShops_CreateListAndDeactivate_SecretIsReturnedOnceAndADisabledShopTakesNoWebhook()
    {
        var branch = Guid.NewGuid();
        var shopId = $"shopee-{Guid.NewGuid():N}";

        var created = await Owner.CreateShopAsync("shopee", $"  {shopId} ", branch);

        // Tên kênh được chuẩn hóa, mã shop được cắt khoảng trắng; secret là 32 byte ngẫu nhiên viết hex.
        Assert.Equal(("Shopee", shopId, branch, true), (created.Channel, created.ShopId, created.BranchId, created.IsActive));
        Assert.Matches("^[0-9a-f]{64}$", created.Secret!);
        var listed = await Owner.GetFromJsonAsync<JsonElement>("/channel-shops");
        var only = Assert.Single(listed.EnumerateArray());
        Assert.Equal(created.Id, only.GetProperty("id").GetGuid());
        Assert.False(only.TryGetProperty("secret", out _));
        var body = Shopee("evt-1", shopId, "ORD-1");
        Assert.Equal("Submitted", await factory.SendWebhookAsync("shopee", body, created.Secret!).AcceptedStatusAsync("evt-1"));

        var disabled = await Owner.PatchAsJsonAsync($"/channel-shops/{created.Id}/active", new { isActive = false });

        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        var dto = (await disabled.Content.ReadFromJsonAsync<ChannelShopDto>())!;
        Assert.Equal((created.Id, false, (string?)null), (dto.Id, dto.IsActive, dto.Secret));
        var refused = await factory.SendWebhookAsync("shopee", Shopee("evt-2", shopId, "ORD-2"), created.Secret!);
        await refused.AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        // Bật lại thì nhận tiếp, vẫn với secret cũ.
        await Owner.PatchAsJsonAsync($"/channel-shops/{created.Id}/active", new { isActive = true });
        Assert.Equal("Submitted", await factory.SendWebhookAsync("shopee", Shopee("evt-2", shopId, "ORD-2"), created.Secret!).AcceptedStatusAsync("evt-2"));
    }

    [Fact]
    [Trait("Scenario", "T14")]
    public async Task ChannelShops_DuplicateBadInputOtherRolesOrAnotherTenant_AreRefused()
    {
        var shop = await Owner.CreateShopAsync("TikTok");
        var otherOwner = factory.CreateClient(Roles.Owner, Guid.NewGuid());
        var duplicate = new { channel = "TikTok", shopId = shop.ShopId, branchId = Guid.NewGuid() };

        // Mã shop là unique theo kênh trên toàn hệ thống: tenant nào tạo lại cũng bị từ chối. Cùng mã ở kênh khác thì được.
        await (await Owner.PostAsJsonAsync("/channel-shops", duplicate)).AssertProblemAsync(HttpStatusCode.Conflict, "duplicate");
        await (await otherOwner.PostAsJsonAsync("/channel-shops", duplicate)).AssertProblemAsync(HttpStatusCode.Conflict, "duplicate");
        await otherOwner.CreateShopAsync("Lazada", shop.ShopId);
        await (await Owner.PostAsJsonAsync("/channel-shops", new { channel = "Zalo", shopId = "s", branchId = Guid.NewGuid() }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await Owner.PostAsJsonAsync("/channel-shops", new { channel = "1", shopId = "s", branchId = Guid.NewGuid() }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await Owner.PostAsJsonAsync("/channel-shops", new { channel = "Shopee", shopId = " ", branchId = Guid.NewGuid() }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        await (await Owner.PostAsJsonAsync("/channel-shops", new { channel = "Shopee", shopId = "s", branchId = Guid.Empty }))
            .AssertProblemAsync(HttpStatusCode.BadRequest, "validation_failed");

        // Tenant khác không thấy và không tắt được shop này.
        Assert.DoesNotContain(
            (await otherOwner.GetFromJsonAsync<IReadOnlyList<ChannelShopDto>>("/channel-shops"))!, listed => listed.Id == shop.Id);
        await (await otherOwner.PatchAsJsonAsync($"/channel-shops/{shop.Id}/active", new { isActive = false }))
            .AssertProblemAsync(HttpStatusCode.NotFound, "not_found");
        Assert.True(Assert.Single((await Owner.GetFromJsonAsync<IReadOnlyList<ChannelShopDto>>("/channel-shops"))!).IsActive);

        // Cấu hình shop và simulator chỉ dành cho Owner (docs/architecture/security.md).
        foreach (var role in new[] { Roles.Staff, Roles.Cashier })
        {
            var client = factory.CreateClient(role, _tenantId);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/channel-shops")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/channel-shops", duplicate)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PatchAsJsonAsync($"/channel-shops/{shop.Id}/active", new { isActive = false })).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/channel-shops")).StatusCode);
    }
}
