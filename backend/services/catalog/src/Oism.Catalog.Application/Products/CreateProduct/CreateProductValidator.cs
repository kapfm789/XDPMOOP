using FluentValidation;

namespace Oism.Catalog.Application.Products.CreateProduct;

public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(command => command.Name).ProductName();
        RuleFor(command => command.Description).ProductDescription();
        // UC-PROD-02 AC-1, AC-2: sản phẩm đơn có đúng một SKU; sản phẩm có biến thể có ít nhất một.
        RuleFor(command => command.Skus)
            .Must((command, skus) => skus is not null && (command.HasVariants ? skus.Count >= 1 : skus.Count == 1))
            .WithMessage("Sản phẩm không có biến thể cần đúng một SKU; sản phẩm có biến thể cần ít nhất một SKU");
        RuleForEach(command => command.Skus).SetValidator(new SkuInputValidator());
    }
}

internal static class ProductRules
{
    public static IRuleBuilderOptions<T, string> ProductName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Không được để trống")
            .MaximumLength(200).WithMessage("Tối đa 200 ký tự");

    public static IRuleBuilderOptions<T, string?> ProductDescription<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(2000).WithMessage("Tối đa 2000 ký tự");
}
