namespace Oism.Seed;

// Chỉ các trường seed cần của phản hồi API: docs/design/api/.
internal sealed record Paged<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

internal sealed record LoginResponse(string AccessToken, string RefreshToken);

internal sealed record UserDto(Guid Id, string? Email);

internal sealed record BranchDto(Guid Id, string Code, string Name);

internal sealed record CategoryDto(Guid Id, string Name, IReadOnlyList<CategoryDto>? Children);

internal sealed record BrandDto(Guid Id, string Name);

internal sealed record ProductRow(Guid Id, string Name);

internal sealed record BarcodeDto(Guid Id, string Code);

internal sealed record SkuDto(Guid Id, string SkuCode, decimal RetailPrice, IReadOnlyList<BarcodeDto> Barcodes);

internal sealed record ProductDto(Guid Id, string Name, IReadOnlyList<SkuDto> Skus);

internal sealed record SupplierDto(Guid Id, string Name);

internal sealed record ReceiptDto(Guid Id, Guid BranchId, string Status, string? Note);

internal sealed record OrderDto(Guid Id, string OrderNumber, decimal TotalAmount, string? Note);

internal sealed record TransferDto(Guid Id, string Status);

internal sealed record ChannelShopDto(Guid Id, string Channel, string ShopId);

internal sealed record GrossProfitTotals(int Quantity, decimal Revenue);

internal sealed record GrossProfitReport(GrossProfitTotals Totals);

internal sealed record ForecastRunDto(DateTimeOffset RanAt, string Status, decimal? ForecastWape, decimal? BaselineWape, string? Error);

internal sealed record ReorderSuggestions(ForecastRunDto? Run, int Total);
