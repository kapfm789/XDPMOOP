using System.Text.Json.Serialization;
using Oism.Channel.Domain;

namespace Oism.Channel.Application.Shops;

// Secret chỉ được trả một lần lúc tạo shop (docs/design/api/channel.md).
public sealed record ChannelShopDto(
    Guid Id,
    string Channel,
    string ShopId,
    Guid BranchId,
    bool IsActive,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Secret = null)
{
    public static ChannelShopDto From(ChannelShop shop, bool includeSecret = false) => new(
        shop.Id, shop.Channel.ToString(), shop.ShopId, shop.BranchId, shop.IsActive, includeSecret ? shop.Secret : null);
}

public interface IChannelShopRepository
{
    // Tìm shop theo kênh và mã shop trên mọi tenant rồi đặt tenant context theo shop tìm được: webhook không mang JWT.
    // Một trong các chỗ được phép bỏ filter (docs/architecture/multi-tenancy.md).
    Task<ChannelShop?> FindForWebhookAsync(SalesChannel channel, string shopId, CancellationToken ct);

    // Shop của tenant hiện tại theo kênh và mã shop.
    Task<ChannelShop?> FindAsync(SalesChannel channel, string shopId, CancellationToken ct);

    Task<ChannelShop?> GetAsync(Guid id, CancellationToken ct);

    // Shop của tenant hiện tại, theo kênh rồi mã shop.
    Task<IReadOnlyList<ChannelShopDto>> ListAsync(CancellationToken ct);

    void Add(ChannelShop shop);
}
