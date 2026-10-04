using FluentValidation;
using Oism.Catalog.Application.Brands;
using Oism.Catalog.Application.Categories;
using Oism.Catalog.Domain;
using Oism.SharedKernel;

namespace Oism.Catalog.Application.Products.CreateProduct;

// UC-PROD-02: tạo sản phẩm đơn hoặc sản phẩm có biến thể, mỗi SKU phát một SkuUpserted.
public sealed class CreateProductHandler(
    IUnitOfWork unitOfWork,
    IProductRepository products,
    ICategoryRepository categories,
    IBrandRepository brands,
    IEventPublisher events)
{
    private static readonly CreateProductValidator Validator = new();

    public async Task<ProductDto> Handle(CreateProductCommand command, CancellationToken ct)
    {
        Validator.ValidateAndThrow(command);

        await using var transaction = await unitOfWork.BeginAsync(ct);

        // Danh mục hoặc thương hiệu của tenant khác cũng là "không tìm thấy" (docs/architecture/multi-tenancy.md).
        if (!await categories.ExistsAsync(command.CategoryId, ct))
            throw new NotFoundException("danh mục");
        if (command.BrandId is { } brandId && await brands.GetAsync(brandId, ct) is null)
            throw new NotFoundException("thương hiệu");

        var product = Product.Create(command.Name, command.CategoryId, command.BrandId, command.Description, command.HasVariants);
        foreach (var input in command.Skus)
        {
            var sku = product.AddSku(
                input.SkuCode, SkuRules.Normalize(input.Attributes),
                command.CanSetPrices ? input.RetailPrice ?? 0 : 0,
                command.CanSetPrices ? input.WholesalePrice ?? 0 : 0);
            events.EnqueueSkuUpserted(product, sku);
        }

        products.Add(product);

        // Trùng mã SKU trong tenant (UC-PROD-02 AC-3): chỉ mục unique từ chối, CommitAsync ném DuplicateException.
        await transaction.CommitAsync(ct);
        return ProductDto.From(product);
    }
}
