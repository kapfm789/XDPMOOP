using Oism.SharedKernel;

namespace Oism.Channel.Application.Shops.SetChannelShopActive;

public sealed record SetChannelShopActiveCommand(Guid Id, bool IsActive);

// Shop bị tắt coi như không có: webhook của nó trả 404 (docs/design/flows/webhook-ingestion.md).
public sealed class SetChannelShopActiveHandler(IUnitOfWork unitOfWork, IChannelShopRepository shops)
{
    public async Task<ChannelShopDto> Handle(SetChannelShopActiveCommand command, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginAsync(ct);

        var shop = await shops.GetAsync(command.Id, ct) ?? throw new NotFoundException("shop");
        shop.SetActive(command.IsActive);

        await transaction.CommitAsync(ct);
        return ChannelShopDto.From(shop);
    }
}
