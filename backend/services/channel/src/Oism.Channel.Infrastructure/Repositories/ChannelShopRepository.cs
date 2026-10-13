using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Tenancy;
using Oism.Channel.Application.Shops;
using Oism.Channel.Domain;

namespace Oism.Channel.Infrastructure.Repositories;

internal sealed class ChannelShopRepository(ChannelDbContext db, ITenantContext tenantContext) : IChannelShopRepository
{
    // Một trong các chỗ được phép bỏ filter: docs/architecture/multi-tenancy.md.
    public async Task<ChannelShop?> FindForWebhookAsync(SalesChannel channel, string shopId, CancellationToken ct)
    {
        var shop = await db.ChannelShops.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Channel == channel && candidate.ShopId == shopId, ct);

        if (shop is not null)
            tenantContext.Set(shop.TenantId);
        return shop;
    }

    public Task<ChannelShop?> FindAsync(SalesChannel channel, string shopId, CancellationToken ct) =>
        db.ChannelShops.AsNoTracking().SingleOrDefaultAsync(shop => shop.Channel == channel && shop.ShopId == shopId, ct);

    public Task<ChannelShop?> GetAsync(Guid id, CancellationToken ct) =>
        db.ChannelShops.SingleOrDefaultAsync(shop => shop.Id == id, ct);

    public async Task<IReadOnlyList<ChannelShopDto>> ListAsync(CancellationToken ct) =>
        (await db.ChannelShops.AsNoTracking().OrderBy(shop => shop.Channel).ThenBy(shop => shop.ShopId).ToListAsync(ct))
            .Select(shop => ChannelShopDto.From(shop))
            .ToList();

    public void Add(ChannelShop shop) => db.Add(shop);
}
