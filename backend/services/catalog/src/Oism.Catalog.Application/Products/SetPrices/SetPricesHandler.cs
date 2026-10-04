using FluentValidation;
using Oism.SharedKernel;

namespace Oism.Catalog.Application.Products.SetPrices;

public sealed record SetPricesCommand(Guid SkuId, decimal RetailPrice, decimal WholesalePrice);

public sealed class SetPricesValidator : AbstractValidator<SetPricesCommand>
{
    public SetPricesValidator()
    {
        RuleFor(command => command.RetailPrice).NonNegativePrice();
        RuleFor(command => command.WholesalePrice).NonNegativePrice();
    }
}

// UC-PROD-04: Owner đặt giá lẻ và giá sỉ niêm yết. Giá vốn thuộc `core`, không nằm ở đây.
public sealed class SetPricesHandler(IUnitOfWork unitOfWork, IProductRepository products, IEventPublisher events)
{
    private static readonly SetPricesValidator Validator = new();

    public async Task<SkuDto> Handle(SetPricesCommand command, CancellationToken ct)
    {
        Validator.ValidateAndThrow(command);

        await using var transaction = await unitOfWork.BeginAsync(ct);

        var product = await products.GetBySkuForUpdateAsync(command.SkuId, ct) ?? throw new NotFoundException("SKU");
        var sku = product.Skus.Single(candidate => candidate.Id == command.SkuId);
        sku.SetPrices(command.RetailPrice, command.WholesalePrice);
        events.EnqueueSkuUpserted(product, sku);

        await transaction.CommitAsync(ct);
        return SkuDto.From(product, sku);
    }
}
