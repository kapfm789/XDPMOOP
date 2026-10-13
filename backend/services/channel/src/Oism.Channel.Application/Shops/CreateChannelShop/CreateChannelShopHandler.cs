using FluentValidation;
using Oism.Channel.Domain;

namespace Oism.Channel.Application.Shops.CreateChannelShop;

public sealed record CreateChannelShopCommand(string? Channel, string? ShopId, Guid BranchId);

public sealed class CreateChannelShopValidator : AbstractValidator<CreateChannelShopCommand>
{
    public CreateChannelShopValidator()
    {
        RuleFor(command => command.Channel)
            .Must(channel => SalesChannels.TryParse(channel, out _)).WithMessage("Kênh là Shopee, TikTok hoặc Lazada");
        RuleFor(command => command.ShopId).NotEmpty().WithMessage("Nhập mã shop").MaximumLength(100);
        RuleFor(command => command.BranchId).NotEmpty().WithMessage("Chọn chi nhánh");
    }
}

// UC-SIM-01: cấu hình một shop của sàn cho tenant hiện tại. `channel` không giữ bản sao chi nhánh:
// chi nhánh không có hoặc đã tắt thì `core` từ chối đơn của shop bằng OrderRejected.
public sealed class CreateChannelShopHandler(IUnitOfWork unitOfWork, IChannelShopRepository shops)
{
    private static readonly CreateChannelShopValidator Validator = new();

    public async Task<ChannelShopDto> Handle(CreateChannelShopCommand command, CancellationToken ct)
    {
        Validator.ValidateAndThrow(command);
        SalesChannels.TryParse(command.Channel, out var channel);

        await using var transaction = await unitOfWork.BeginAsync(ct);

        var shop = ChannelShop.Create(channel, command.ShopId!, command.BranchId);
        shops.Add(shop);

        // Mã shop đã có ở bất kỳ tenant nào: chỉ mục unique (channel, shop_id) từ chối, CommitAsync ném DuplicateException.
        await transaction.CommitAsync(ct);
        return ChannelShopDto.From(shop, includeSecret: true);
    }
}
