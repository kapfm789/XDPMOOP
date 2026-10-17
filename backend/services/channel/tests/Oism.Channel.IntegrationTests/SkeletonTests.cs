using System.Net;
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
