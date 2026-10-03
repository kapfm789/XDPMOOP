using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Persistence;
using Oism.BuildingBlocks.Tenancy;

namespace Oism.Channel.Infrastructure;

public sealed class ChannelDbContext(DbContextOptions<ChannelDbContext> options, ITenantContext tenant)
    : OismDbContext(options, tenant, Schema)
{
    public const string Schema = "channel";
}
