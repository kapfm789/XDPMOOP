using FluentValidation;
using Oism.SharedKernel;

namespace Oism.Catalog.Application.Products.UpdateSku;

public sealed record UpdateSkuCommand(Guid SkuId, IReadOnlyDictionary<string, string>? Attributes, bool IsActive);

public sealed class UpdateSkuValidator : AbstractValidator<UpdateSkuCommand>
{
    public UpdateSkuValidator() => RuleFor(command => command.Attributes).ValidAttributes();
}

// UC-PROD-02 AC-4, AC-5: sửa thuộc tính hoặc ngừng bán một SKU.
public sealed class UpdateSkuHandler(IUnitOfWork unitOfWork, IProductRepository products, IEventPublisher events)
{
    private static readonly UpdateSkuValidator Validator = new();

    public async Task<SkuDto> Handle(UpdateSkuCommand command, CancellationToken ct)
    {
        Validator.ValidateAndThrow(command);

        await using var transaction = await unitOfWork.BeginAsync(ct);

        var product = await products.GetBySkuForUpdateAsync(command.SkuId, ct) ?? throw new NotFoundException("SKU");
        var sku = product.Skus.Single(candidate => candidate.Id == command.SkuId);
        sku.Update(SkuRules.Normalize(command.Attributes), command.IsActive);
        events.EnqueueSkuUpserted(product, sku);

        await transaction.CommitAsync(ct);
        return SkuDto.From(product, sku);
    }
}
