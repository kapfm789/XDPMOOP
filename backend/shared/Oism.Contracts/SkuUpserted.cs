namespace Oism.Contracts;

// docs/design/events.md mục "SkuUpserted". Bên nhận bỏ qua bản có Version nhỏ hơn hoặc bằng bản đang giữ.
public sealed record SkuUpserted(
    Guid SkuId,
    Guid ProductId,
    string SkuCode,
    string Name,
    IReadOnlyList<string> Barcodes,
    decimal RetailPrice,
    decimal WholesalePrice,
    bool IsActive,
    long Version);
