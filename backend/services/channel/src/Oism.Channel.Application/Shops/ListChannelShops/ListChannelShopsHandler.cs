namespace Oism.Channel.Application.Shops.ListChannelShops;

public sealed class ListChannelShopsHandler(IChannelShopRepository shops)
{
    public Task<IReadOnlyList<ChannelShopDto>> Handle(CancellationToken ct) => shops.ListAsync(ct);
}
