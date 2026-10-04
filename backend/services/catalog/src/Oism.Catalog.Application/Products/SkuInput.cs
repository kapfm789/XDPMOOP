using FluentValidation;

namespace Oism.Catalog.Application.Products;

// Một SKU gửi lên khi tạo sản phẩm hoặc thêm SKU: docs/design/api/catalog.md mục "Sản phẩm và SKU".
public sealed record SkuInput(
    string SkuCode, IReadOnlyDictionary<string, string>? Attributes, decimal? RetailPrice, decimal? WholesalePrice);

public sealed class SkuInputValidator : AbstractValidator<SkuInput>
{
    public SkuInputValidator()
    {
        RuleFor(sku => sku.SkuCode)
            .NotEmpty().WithMessage("Không được để trống")
            .MaximumLength(64).WithMessage("Tối đa 64 ký tự");
        RuleFor(sku => sku.Attributes).ValidAttributes();
        RuleFor(sku => sku.RetailPrice).NonNegativePrice();
        RuleFor(sku => sku.WholesalePrice).NonNegativePrice();
    }
}

internal static class SkuRules
{
    private const int MaxAttributeLength = 100;

    public static IRuleBuilderOptions<T, IReadOnlyDictionary<string, string>?> ValidAttributes<T>(
        this IRuleBuilder<T, IReadOnlyDictionary<string, string>?> rule) =>
        rule.Must(attributes => attributes is null
                || (attributes.All(attribute => IsFilled(attribute.Key) && IsFilled(attribute.Value))
                    && attributes.Keys.Select(key => key.Trim()).Distinct().Count() == attributes.Count))
            .WithMessage($"Tên thuộc tính không được trùng; tên và giá trị không được trống, tối đa {MaxAttributeLength} ký tự");

    // UC-PROD-04 AC-2: giá âm bị từ chối.
    public static IRuleBuilderOptions<T, decimal?> NonNegativePrice<T>(this IRuleBuilder<T, decimal?> rule) =>
        rule.GreaterThanOrEqualTo(0).WithMessage("Giá không được âm");

    public static IRuleBuilderOptions<T, decimal> NonNegativePrice<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.GreaterThanOrEqualTo(0).WithMessage("Giá không được âm");

    // Cắt khoảng trắng tên và giá trị; null coi như không có thuộc tính.
    public static IReadOnlyDictionary<string, string> Normalize(IReadOnlyDictionary<string, string>? attributes) =>
        (attributes ?? new Dictionary<string, string>())
        .ToDictionary(attribute => attribute.Key.Trim(), attribute => attribute.Value.Trim());

    private static bool IsFilled(string? text) => !string.IsNullOrWhiteSpace(text) && text.Length <= MaxAttributeLength;
}
