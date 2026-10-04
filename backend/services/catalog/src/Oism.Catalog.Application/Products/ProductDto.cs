using Oism.Catalog.Domain;
using Oism.Contracts;

namespace Oism.Catalog.Application.Products;

public sealed record BarcodeDto(Guid Id, string Code, string Symbology)
{
    public static BarcodeDto From(Barcode barcode) => new(barcode.Id, barcode.Code, barcode.Symbology.ToString());
}

// `Name` là tên hiển thị: tên sản phẩm kèm các giá trị thuộc tính.
public sealed record SkuDto(
    Guid Id,
    Guid ProductId,
    string SkuCode,
    string Name,
    IReadOnlyDictionary<string, string> Attributes,
    decimal RetailPrice,
    decimal WholesalePrice,
    bool IsActive,
    IReadOnlyList<BarcodeDto> Barcodes)
{
    public static SkuDto From(Product product, Sku sku) => new(
        sku.Id, product.Id, sku.SkuCode, product.DisplayNameOf(sku), sku.Attributes, sku.RetailPrice, sku.WholesalePrice, sku.IsActive,
        sku.Barcodes.OrderBy(barcode => barcode.Code, StringComparer.Ordinal).Select(BarcodeDto.From).ToList());
}

// Sản phẩm kèm mọi SKU, mã vạch và giá: GET /products/{id}.
public sealed record ProductDto(
    Guid Id,
    string Name,
    Guid CategoryId,
    Guid? BrandId,
    string? Description,
    bool HasVariants,
    bool IsActive,
    IReadOnlyList<SkuDto> Skus)
{
    public static ProductDto From(Product product) => new(
        product.Id, product.Name, product.CategoryId, product.BrandId, product.Description, product.HasVariants, product.IsActive,
        product.Skus.OrderBy(sku => sku.SkuCode, StringComparer.Ordinal).Select(sku => SkuDto.From(product, sku)).ToList());
}

// Một dòng của GET /products.
public sealed record ProductListItemDto(
    Guid Id, string Name, Guid CategoryId, Guid? BrandId, bool HasVariants, bool IsActive, int SkuCount);

internal static class SkuUpsertedFactory
{
    // Thông điệp trạng thái: mang toàn bộ trạng thái hiện tại của SKU (docs/design/events.md).
    public static SkuUpserted From(Product product, Sku sku) => new(
        sku.Id, product.Id, sku.SkuCode, product.DisplayNameOf(sku),
        sku.Barcodes.Select(barcode => barcode.Code).Order(StringComparer.Ordinal).ToList(),
        sku.RetailPrice, sku.WholesalePrice, product.IsSellable(sku), sku.Version);

    // Gọi sau thay đổi cuối cùng của use case, để version trong thông điệp là version được lưu.
    public static void EnqueueSkuUpserted(this IEventPublisher events, Product product, Sku sku) =>
        events.Enqueue(From(product, sku));
}
