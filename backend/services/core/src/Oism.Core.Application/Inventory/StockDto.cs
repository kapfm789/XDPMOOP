using System.Text.Json.Serialization;
using Oism.Core.Domain.Inventory;

namespace Oism.Core.Application.Inventory;

// Một dòng của GET /stock. AvgCost chỉ có với Owner; với vai trò khác trường này không nằm trong phản hồi
// (docs/design/api/core.md mục "Tồn và sổ").
public sealed record StockDto(
    Guid BranchId,
    Guid SkuId,
    string SkuCode,
    string Name,
    int OnHand,
    int Reserved,
    int Available,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? AvgCost,
    int? Threshold);

// Một dòng của GET /ledger. UnitCost chỉ có với Owner.
public sealed record LedgerLineDto(
    long Seq,
    Guid Id,
    Guid BranchId,
    Guid SkuId,
    string SkuCode,
    string Name,
    string Type,
    string Reason,
    int Quantity,
    int BalanceAfter,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? UnitCost,
    string ReferenceType,
    Guid ReferenceId,
    Guid? ReversalOfId,
    Guid? CreatedBy,
    DateTimeOffset CreatedAt)
{
    public static LedgerLineDto From(InventoryTransaction line, string skuCode, string name, bool includeCost) => new(
        line.Seq, line.Id, line.BranchId, line.SkuId, skuCode, name, line.Type.ToString(), line.Reason.ToString(),
        line.Quantity, line.BalanceAfter, includeCost ? line.UnitCost : null, line.ReferenceType.ToString(), line.ReferenceId,
        line.ReversalOfId, line.CreatedBy, line.CreatedAt);
}

// Truy vấn chỉ đọc của Inventory, cài đặt ở Infrastructure.
public interface IStockQueries
{
    // Số dư kèm mã và tên SKU, xếp theo mã SKU rồi chi nhánh.
    Task<PagedResult<StockDto>> ListStockAsync(ListStock.ListStockQuery query, CancellationToken ct);

    // Dòng sổ theo thứ tự ghi (seq tăng dần).
    Task<PagedResult<LedgerLineDto>> ListLedgerAsync(ListLedger.ListLedgerQuery query, CancellationToken ct);

    // Nhà cung cấp của tenant, xếp theo tên.
    Task<IReadOnlyList<SupplierDto>> ListSuppliersAsync(CancellationToken ct);

    // Phiếu nhập kèm các dòng, mới nhất trước.
    Task<PagedResult<PurchaseReceiptDto>> ListPurchaseReceiptsAsync(
        ListPurchaseReceipts.ListPurchaseReceiptsQuery query, CancellationToken ct);

    // Null khi phiếu không có trong tenant.
    Task<PurchaseReceiptDto?> FindPurchaseReceiptAsync(Guid id, CancellationToken ct);

    // Phiếu chuyển kho kèm các dòng: phiếu chưa xuất đứng trước, rồi tới phiếu xuất gần nhất.
    Task<PagedResult<TransferDto>> ListTransfersAsync(ListTransfers.ListTransfersQuery query, CancellationToken ct);

    // SKU đang bán khớp một phần tên, mã SKU hoặc mã vạch, kèm tồn khả dụng tại chi nhánh;
    // SKU khớp đúng mã hoặc mã vạch đứng trước, rồi theo mã SKU.
    Task<IReadOnlyList<Orders.SearchPosSkus.PosSkuDto>> SearchPosSkusAsync(
        Orders.SearchPosSkus.SearchPosSkusQuery query, int limit, CancellationToken ct);
}
