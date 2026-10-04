using Oism.Core.Domain.Inventory;

namespace Oism.Core.Application.Inventory;

public sealed record SupplierDto(Guid Id, string Name, string? Phone, bool IsActive)
{
    public static SupplierDto From(Supplier supplier) => new(supplier.Id, supplier.Name, supplier.Phone, supplier.IsActive);
}

// Mã và tên SKU lấy từ sku_refs lúc đọc.
public sealed record PurchaseReceiptItemDto(Guid Id, Guid SkuId, string SkuCode, string SkuName, int Quantity, decimal UnitCost);

// Phiếu nhập kèm các dòng: docs/design/api/core.md mục "Nhập hàng".
public sealed record PurchaseReceiptDto(
    Guid Id,
    string ReceiptNumber,
    Guid BranchId,
    Guid SupplierId,
    string Status,
    string? Note,
    DateTimeOffset? ConfirmedAt,
    Guid? ConfirmedBy,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PurchaseReceiptItemDto> Items);
