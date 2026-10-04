namespace Oism.Catalog.Application.Products.CreateProduct;

// CanSetPrices: chỉ Owner đặt được giá; với vai trò khác, giá gửi kèm bị bỏ qua và đặt bằng 0.
public sealed record CreateProductCommand(
    string Name,
    Guid CategoryId,
    Guid? BrandId,
    string? Description,
    bool HasVariants,
    IReadOnlyList<SkuInput> Skus,
    bool CanSetPrices);
