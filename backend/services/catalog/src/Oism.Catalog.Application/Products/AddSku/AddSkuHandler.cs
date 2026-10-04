using FluentValidation;
using FluentValidation.Results;
using Oism.SharedKernel;

namespace Oism.Catalog.Application.Products.AddSku;

// CanSetPrices: chỉ Owner đặt được giá; với vai trò khác, giá gửi kèm bị bỏ qua và đặt bằng 0.
public sealed record AddSkuCommand(Guid ProductId, SkuInput Sku, bool CanSetPrices);

// UC-PROD-02 AC-2: thêm một biến thể cho sản phẩm.
public sealed class AddSkuHandler(IUnitOfWork unitOfWork, IProductRepository products, IEventPublisher events)
{
    private static readonly SkuInputValidator Validator = new();

    public async Task<SkuDto> Handle(AddSkuCommand command, CancellationToken ct)
    {
        Validator.ValidateAndThrow(command.Sku);

        await using var transaction = await unitOfWork.BeginAsync(ct);

        var product = await products.GetForUpdateAsync(command.ProductId, ct) ?? throw new NotFoundException("sản phẩm");
        if (!product.CanAddSku)
        {
            throw new ValidationException([new ValidationFailure(
                nameof(command.Sku.SkuCode), "Sản phẩm không có biến thể chỉ có một SKU")]);
        }

        var sku = product.AddSku(
            command.Sku.SkuCode, SkuRules.Normalize(command.Sku.Attributes),
            command.CanSetPrices ? command.Sku.RetailPrice ?? 0 : 0,
            command.CanSetPrices ? command.Sku.WholesalePrice ?? 0 : 0);
        events.EnqueueSkuUpserted(product, sku);

        // Trùng mã SKU trong tenant (UC-PROD-02 AC-3): chỉ mục unique từ chối, CommitAsync ném DuplicateException.
        await transaction.CommitAsync(ct);
        return SkuDto.From(product, sku);
    }
}
