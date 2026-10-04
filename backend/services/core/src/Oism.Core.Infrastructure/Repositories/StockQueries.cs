using Microsoft.EntityFrameworkCore;
using Oism.Core.Application;
using Oism.Core.Application.Inventory;
using Oism.Core.Application.Inventory.ListLedger;
using Oism.Core.Application.Inventory.ListPurchaseReceipts;
using Oism.Core.Application.Inventory.ListStock;
using Oism.Core.Domain.Inventory;

namespace Oism.Core.Infrastructure.Repositories;

// Chỉ đọc. Mã và tên SKU lấy từ sku_refs của chính schema core.
internal sealed class StockQueries(CoreDbContext db) : IStockQueries
{
    public async Task<PagedResult<StockDto>> ListStockAsync(ListStockQuery query, CancellationToken ct)
    {
        var rows =
            from balance in db.InventoryBalances.AsNoTracking()
            join sku in db.SkuRefs.AsNoTracking() on balance.SkuId equals sku.SkuId
            where (query.BranchId == null || balance.BranchId == query.BranchId)
                && (query.SkuId == null || balance.SkuId == query.SkuId)
            select new { balance, sku };
        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            var pattern = $"%{query.Query.Trim()}%";
            rows = rows.Where(row => EF.Functions.ILike(row.sku.SkuCode, pattern) || EF.Functions.ILike(row.sku.Name, pattern));
        }

        var includeCost = query.IncludeCost;
        var items = await rows
            .OrderBy(row => row.sku.SkuCode).ThenBy(row => row.balance.BranchId)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(row => new StockDto(
                row.balance.BranchId, row.balance.SkuId, row.sku.SkuCode, row.sku.Name,
                row.balance.OnHand, row.balance.Reserved, row.balance.OnHand - row.balance.Reserved,
                includeCost ? row.balance.AvgCost : null, row.balance.ReorderThreshold))
            .ToListAsync(ct);
        return new PagedResult<StockDto>(items, query.Page, query.PageSize, await rows.CountAsync(ct));
    }

    public async Task<PagedResult<LedgerLineDto>> ListLedgerAsync(ListLedgerQuery query, CancellationToken ct)
    {
        var rows =
            from line in db.InventoryTransactions.AsNoTracking()
            join sku in db.SkuRefs.AsNoTracking() on line.SkuId equals sku.SkuId
            where (query.BranchId == null || line.BranchId == query.BranchId)
                && (query.SkuId == null || line.SkuId == query.SkuId)
                && (query.From == null || line.CreatedAt >= query.From)
                && (query.To == null || line.CreatedAt <= query.To)
            select new { line, sku.SkuCode, sku.Name };

        // Thứ tự của sổ là theo seq, không theo created_at.
        var page = await rows
            .OrderBy(row => row.line.Seq)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(ct);
        return new PagedResult<LedgerLineDto>(
            page.Select(row => LedgerLineDto.From(row.line, row.SkuCode, row.Name, query.IncludeCost)).ToList(),
            query.Page, query.PageSize, await rows.CountAsync(ct));
    }

    public async Task<IReadOnlyList<SupplierDto>> ListSuppliersAsync(CancellationToken ct) =>
        await db.Suppliers.AsNoTracking()
            .OrderBy(supplier => supplier.Name)
            .Select(supplier => new SupplierDto(supplier.Id, supplier.Name, supplier.Phone, supplier.IsActive))
            .ToListAsync(ct);

    public async Task<PagedResult<PurchaseReceiptDto>> ListPurchaseReceiptsAsync(ListPurchaseReceiptsQuery query, CancellationToken ct)
    {
        var status = query.Status is null ? (PurchaseReceiptStatus?)null : Enum.Parse<PurchaseReceiptStatus>(query.Status);
        var receipts = db.PurchaseReceipts.AsNoTracking().Where(receipt =>
            (query.BranchId == null || receipt.BranchId == query.BranchId) && (status == null || receipt.Status == status));

        var page = await receipts
            .OrderByDescending(receipt => receipt.CreatedAt).ThenBy(receipt => receipt.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Include(receipt => receipt.Items)
            .ToListAsync(ct);
        return new PagedResult<PurchaseReceiptDto>(await ToDtosAsync(page, ct), query.Page, query.PageSize, await receipts.CountAsync(ct));
    }

    public async Task<PurchaseReceiptDto?> FindPurchaseReceiptAsync(Guid id, CancellationToken ct)
    {
        var receipts = await db.PurchaseReceipts.AsNoTracking()
            .Where(receipt => receipt.Id == id).Include(receipt => receipt.Items).ToListAsync(ct);
        return (await ToDtosAsync(receipts, ct)).SingleOrDefault();
    }

    private async Task<List<PurchaseReceiptDto>> ToDtosAsync(List<PurchaseReceipt> receipts, CancellationToken ct)
    {
        var skuIds = receipts.SelectMany(receipt => receipt.Items).Select(item => item.SkuId).Distinct().ToList();
        var skus = await db.SkuRefs.AsNoTracking().Where(sku => skuIds.Contains(sku.SkuId)).ToDictionaryAsync(sku => sku.SkuId, ct);

        return receipts.Select(receipt => new PurchaseReceiptDto(
            receipt.Id, receipt.ReceiptNumber, receipt.BranchId, receipt.SupplierId, receipt.Status.ToString(), receipt.Note,
            receipt.ConfirmedAt, receipt.ConfirmedBy, receipt.CreatedAt,
            receipt.Items
                .Select(item => new PurchaseReceiptItemDto(
                    item.Id, item.SkuId, skus[item.SkuId].SkuCode, skus[item.SkuId].Name, item.Quantity, item.UnitCost))
                .OrderBy(item => item.SkuCode).ThenBy(item => item.Id)
                .ToList())).ToList();
    }
}
