using FluentValidation;
using Oism.Catalog.Application.Brands;
using Oism.Catalog.Application.Categories;
using Oism.Catalog.Application.Products.CreateProduct;
using Oism.SharedKernel;

namespace Oism.Catalog.Application.Products.UpdateProduct;

public sealed record UpdateProductCommand(
    Guid Id, string Name, Guid CategoryId, Guid? BrandId, string? Description, bool IsActive);

public sealed class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator()
    {
        RuleFor(command => command.Name).ProductName();
        RuleFor(command => command.Description).ProductDescription();
    }
}

public sealed class UpdateProductHandler(
    IUnitOfWork unitOfWork,
    IProductRepository products,
    ICategoryRepository categories,
    IBrandRepository brands,
    IEventPublisher events)
{
    private static readonly UpdateProductValidator Validator = new();

    public async Task<ProductDto> Handle(UpdateProductCommand command, CancellationToken ct)
    {
        Validator.ValidateAndThrow(command);

        await using var transaction = await unitOfWork.BeginAsync(ct);

        var product = await products.GetForUpdateAsync(command.Id, ct) ?? throw new NotFoundException("sản phẩm");
        if (!await categories.ExistsAsync(command.CategoryId, ct))
            throw new NotFoundException("danh mục");
        if (command.BrandId is { } brandId && await brands.GetAsync(brandId, ct) is null)
            throw new NotFoundException("thương hiệu");

        // Tên hiển thị và trạng thái bán của SKU lấy từ sản phẩm: chúng đổi thì mọi SKU phát lại SkuUpserted.
        if (product.Update(command.Name, command.CategoryId, command.BrandId, command.Description, command.IsActive))
        {
            foreach (var sku in product.Skus)
                events.EnqueueSkuUpserted(product, sku);
        }

        await transaction.CommitAsync(ct);
        return ProductDto.From(product);
    }
}
