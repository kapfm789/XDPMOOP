using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Messaging;
using Oism.Channel.Infrastructure;
using Oism.SharedKernel;

namespace Oism.Channel.IntegrationTests;

public sealed class SkeletonTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_ServiceStarted_Returns200()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // T21: Swagger sinh từ code ghi vai trò và yêu cầu Bearer khớp cột "Vai trò" của docs/design/api/.
    [Fact]
    [Trait("Scenario", "T21")]
    public async Task Swagger_Document_MarksRolesAndPublicEndpoints()
    {
        var paths = (await factory.CreateClient().GetFromJsonAsync<JsonObject>("/swagger/v1/swagger.json"))!["paths"]!;

        Assert.Equal("Vai trò: Owner.", (string?)paths["/channel-shops"]!["get"]!["description"]);
        Assert.Equal("Công khai, không cần token.", (string?)paths["/webhooks/{channel}"]!["post"]!["description"]);
        Assert.Null(paths["/webhooks/{channel}"]!["post"]!["security"]);
    }

    // docs/architecture/multi-tenancy.md: entity không có tenant_id phải được loại trừ tường minh ở đây.
    // inbox_messages là bảng hạ tầng: khóa là eventId của thông điệp, không thuộc tenant nào.
    [Fact]
    [Trait("Scenario", "T14")]
    public void DbContext_EveryEntity_ImplementsITenantOwned()
    {
        using var scope = factory.Services.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<ChannelDbContext>().Model;

        Assert.DoesNotContain(model.GetEntityTypes(), entity =>
            entity.ClrType != typeof(InboxMessage) && !typeof(ITenantOwned).IsAssignableFrom(entity.ClrType));
    }
}
