using System.Text;
using FluentValidation;
using Oism.Channel.Application.Shops;
using Oism.Channel.Application.Webhooks.Adapters;
using Oism.Channel.Domain;
using Oism.SharedKernel;

namespace Oism.Channel.Application.Simulator.SimulateBurst;

// UnitPrice bỏ trống là 0: `channel` không biết giá của SKU.
public sealed record SimulateBurstCommand(
    string? Channel, string? ShopId, string? SkuCode, int QuantityPerOrder, int Orders, int Concurrency, decimal? UnitPrice);

public sealed record BurstResult(Guid BurstId, int Sent);

// Gửi một webhook vào chính endpoint POST /webhooks/{channel} của service, kèm header X-Signature.
public interface IWebhookSender
{
    Task SendAsync(SalesChannel channel, byte[] body, string signature, CancellationToken ct);
}

public sealed class SimulateBurstValidator : AbstractValidator<SimulateBurstCommand>
{
    public SimulateBurstValidator()
    {
        RuleFor(command => command.Channel)
            .Must(channel => SalesChannels.TryParse(channel, out _)).WithMessage("Kênh là Shopee, TikTok hoặc Lazada");
        RuleFor(command => command.ShopId).NotEmpty().WithMessage("Nhập mã shop");
        RuleFor(command => command.SkuCode).NotEmpty().WithMessage("Nhập mã SKU");
        RuleFor(command => command.QuantityPerOrder).GreaterThan(0).WithMessage("Số lượng mỗi đơn phải dương");
        RuleFor(command => command.Orders).InclusiveBetween(1, 1000).WithMessage("Số đơn từ 1 đến 1000");
        RuleFor(command => command.Concurrency).InclusiveBetween(1, 100).WithMessage("Số webhook gửi cùng lúc từ 1 đến 100");
        RuleFor(command => command.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("Đơn giá không được âm");
    }
}

// UC-SIM-01 AC-5: công cụ bắn tải. Tự sinh webhook theo hình dạng của sàn, mỗi webhook một mã sự kiện và một mã đơn riêng,
// ký bằng secret của shop rồi gửi song song vào chính endpoint webhook (docs/design/flows/webhook-ingestion.md).
public sealed class SimulateBurstHandler(IChannelShopRepository shops, IWebhookSender sender, IClock clock)
{
    private static readonly SimulateBurstValidator Validator = new();

    public async Task<BurstResult> Handle(SimulateBurstCommand command, CancellationToken ct)
    {
        Validator.ValidateAndThrow(command);
        SalesChannels.TryParse(command.Channel, out var channel);

        var shop = await shops.FindAsync(channel, command.ShopId!.Trim(), ct) ?? throw new NotFoundException("shop");
        var adapter = ChannelAdapter.For(channel);
        var burstId = Guid.NewGuid();
        var prefix = burstId.ToString("N")[..8].ToUpperInvariant();
        var placedAt = clock.UtcNow;

        var sent = 0;
        await Parallel.ForEachAsync(
            Enumerable.Range(1, command.Orders),
            new ParallelOptions { MaxDegreeOfParallelism = command.Concurrency, CancellationToken = ct },
            async (index, token) =>
            {
                var order = CanonicalOrder.From(
                    $"SIM{prefix}-{index:0000}", placedAt,
                    [(command.SkuCode, command.QuantityPerOrder, command.UnitPrice ?? 0, 0)]);
                var body = Encoding.UTF8.GetBytes(adapter.Build(new WebhookEnvelope($"sim-{prefix}-{index:0000}", shop.ShopId), order));
                await sender.SendAsync(channel, body, shop.Sign(body), token);
                Interlocked.Increment(ref sent);
            });

        return new BurstResult(burstId, sent);
    }
}
