using System.Security.Cryptography;
using System.Text;
using Oism.Channel.Domain;

namespace Oism.Channel.UnitTests.Webhooks;

// Máy trạng thái của bản ghi webhook (docs/design/state-machines.md mục "Bản ghi webhook") và chữ ký của shop.
public sealed class WebhookEventTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 3, 15, 27, TimeSpan.Zero);

    [Fact]
    [Trait("UseCase", "UC-SIM-01 AC-6")]
    public void Lifecycle_SubmittedThenReservedOrRejected_AndFailedIsFinal()
    {
        var orderId = Guid.NewGuid();
        var reserved = WebhookEvent.Receive(SalesChannel.Shopee, "evt-1", "{}", Now);
        var rejected = WebhookEvent.Receive(SalesChannel.TikTok, "evt-2", "{}", Now);
        var failed = WebhookEvent.Receive(SalesChannel.Lazada, "evt-3", "{}", Now);
        Assert.Equal((WebhookStatus.Received, Now, Now), (reserved.Status, reserved.ReceivedAt, reserved.UpdatedAt));

        reserved.MarkSubmitted("ORD-1", Now);
        reserved.MarkReserved(orderId, Now.AddSeconds(2));
        rejected.MarkSubmitted("ORD-2", Now);
        rejected.MarkRejected("InsufficientStock", Now.AddSeconds(3));
        failed.MarkFailed("Đơn không có dòng hàng", Now);

        Assert.Equal(
            (WebhookStatus.Reserved, "ORD-1", (Guid?)orderId, (string?)null, Now, Now.AddSeconds(2)),
            (reserved.Status, reserved.ExternalOrderId, reserved.OrderId, reserved.Reason, reserved.ReceivedAt, reserved.UpdatedAt));
        Assert.Equal((WebhookStatus.Rejected, "InsufficientStock", (Guid?)null), (rejected.Status, rejected.Reason, rejected.OrderId));
        Assert.Equal((WebhookStatus.Failed, "Đơn không có dòng hàng", (string?)null), (failed.Status, failed.Reason, failed.ExternalOrderId));
        // Bước không có trong bảng bị từ chối.
        Assert.Throws<InvalidStateTransitionException>(() => reserved.MarkRejected("InsufficientStock", Now));
        Assert.Throws<InvalidStateTransitionException>(() => rejected.MarkReserved(orderId, Now));
        Assert.Throws<InvalidStateTransitionException>(() => failed.MarkSubmitted("ORD-3", Now));
        Assert.Throws<InvalidStateTransitionException>(() => WebhookEvent.Receive(SalesChannel.Shopee, "evt-4", "{}", Now).MarkReserved(orderId, Now));
    }

    [Fact]
    [Trait("UseCase", "UC-SIM-01 AC-2")]
    public void Verifies_OnlyTheLowercaseHexHmacSha256OfTheExactBody()
    {
        var shop = ChannelShop.Create(SalesChannel.Shopee, " shop-1 ", Guid.NewGuid());
        var other = ChannelShop.Create(SalesChannel.Shopee, "shop-2", Guid.NewGuid());
        var body = Encoding.UTF8.GetBytes("""{"event_id":"e"}""");
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(shop.Secret), body)).ToLowerInvariant();

        Assert.Equal(("shop-1", true), (shop.ShopId, shop.IsActive));
        Assert.Matches("^[0-9a-f]{64}$", shop.Secret);
        Assert.NotEqual(shop.Secret, other.Secret);
        Assert.Equal(expected, shop.Sign(body));
        Assert.True(shop.Verifies(body, expected));
        Assert.False(shop.Verifies(body, expected.ToUpperInvariant()));
        Assert.False(shop.Verifies(Encoding.UTF8.GetBytes("""{"event_id":"e" }"""), expected));
        Assert.False(shop.Verifies(body, other.Sign(body)));
        Assert.False(shop.Verifies(body, signature: null));
        Assert.False(shop.Verifies(body, string.Empty));
    }

    [Theory]
    [InlineData("shopee", true, SalesChannel.Shopee)]
    [InlineData("TikTok", true, SalesChannel.TikTok)]
    [InlineData("LAZADA", true, SalesChannel.Lazada)]
    [InlineData("zalo", false, default(SalesChannel))]
    [InlineData("1", false, default(SalesChannel))]
    [InlineData(null, false, default(SalesChannel))]
    public void SalesChannels_TryParse_TakesNamesInAnyCaseButNoNumbers(string? value, bool expected, SalesChannel channel)
    {
        Assert.Equal(expected, SalesChannels.TryParse(value, out var parsed));
        Assert.Equal(channel, parsed);
    }
}
