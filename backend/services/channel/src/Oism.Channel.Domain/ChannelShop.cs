using System.Security.Cryptography;
using System.Text;
using Oism.SharedKernel;

namespace Oism.Channel.Domain;

// Ánh xạ một shop trên sàn sang tenant và chi nhánh, vì webhook không mang JWT:
// docs/design/data-model/channel.md mục "channel_shops".
public sealed class ChannelShop : ITenantOwned
{
    private ChannelShop()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public SalesChannel Channel { get; private set; }

    // Mã shop do sàn cấp; unique theo kênh trên toàn hệ thống.
    public string ShopId { get; private set; } = null!;

    // Chi nhánh xuất hàng cho đơn của shop.
    public Guid BranchId { get; private set; }

    public string Secret { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public static ChannelShop Create(SalesChannel channel, string shopId, Guid branchId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shopId);

        return new ChannelShop
        {
            Id = Guid.NewGuid(),
            Channel = channel,
            ShopId = shopId.Trim(),
            BranchId = branchId,
            Secret = RandomNumberGenerator.GetHexString(64, lowercase: true),
            IsActive = true,
        };
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    // Chữ ký giả lập: HMAC-SHA256 của nguyên văn body với secret của shop, hex chữ thường
    // (docs/design/api/channel.md mục "Chữ ký").
    public string Sign(ReadOnlySpan<byte> body) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), body)).ToLowerInvariant();

    public bool Verifies(ReadOnlySpan<byte> body, string? signature) =>
        signature is not null
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Sign(body)), Encoding.UTF8.GetBytes(signature));
}
