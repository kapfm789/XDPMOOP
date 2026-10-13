using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Messaging;
using Oism.BuildingBlocks.Persistence;
using Oism.BuildingBlocks.Tenancy;
using Oism.Channel.Domain;

namespace Oism.Channel.Infrastructure;

public sealed class ChannelDbContext(DbContextOptions<ChannelDbContext> options, ITenantContext tenant)
    : OismDbContext(options, tenant, Schema), IHasOutbox, IHasInbox
{
    public const string Schema = "channel";

    public DbSet<ChannelShop> ChannelShops => Set<ChannelShop>();

    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
}
