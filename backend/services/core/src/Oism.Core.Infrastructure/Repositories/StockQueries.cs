using Microsoft.EntityFrameworkCore;
using Oism.Core.Application;
using Oism.Core.Application.Inventory;
using Oism.Core.Application.Inventory.ListLedger;
using Oism.Core.Application.Inventory.ListPurchaseReceipts;
using Oism.Core.Application.Inventory.ListStock;
using Oism.Core.Application.Inventory.ListTransfers;
using Oism.Core.Application.Orders.SearchPosSkus;
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

    public async Task<PagedResult<TransferDto>> ListTransfersAsync(ListTransfersQuery query, CancellationToken ct)
    {
        var status = query.Status is null ? (StockTransferStatus?)null : Enum.Parse<StockTransferStatus>(query.Status);
        var transfers = db.StockTransfers.AsNoTracking().Where(transfer => status == null || transfer.Status == status);

        // Phiếu chưa xuất (shipped_at null) đứng trước vì còn việc phải làm, rồi tới phiếu xuất gần nhất.
        var page = await transfers
            .OrderByDescending(transfer => transfer.ShippedAt == null).ThenByDescending(transfer => transfer.ShippedAt)
            .ThenBy(transfer => transfer.TransferNumber)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Include(transfer => transfer.Items)
            .ToListAsync(ct);

        var skuIds = page.SelectMany(transfer => transfer.Items).Select(item => item.SkuId).Distinct().ToList();
        var skus = await db.SkuRefs.AsNoTracking().Where(sku => skuIds.Contains(sku.SkuId)).ToDictionaryAsync(sku => sku.SkuId, ct);
        return new PagedResult<TransferDto>(
            page.Select(transfer => TransferDto.From(transfer, skus, query.IncludeCost)).ToList(),
            query.Page, query.PageSize, await transfers.CountAsync(ct));
    }

    public async Task<IReadOnlyList<PosSkuDto>> SearchPosSkusAsync(SearchPosSkusQuery query, int limit, CancellationToken ct)
    {
        // UC-POS-01 AC-5: SKU đã ngừng bán không hiện.
        var skus = db.SkuRefs.AsNoTracking().Where(sku => sku.IsActive);
        if (string.IsNullOrWhiteSpace(query.Query))
        {
            skus = skus.OrderBy(sku => sku.SkuCode);
        }
        else
        {
            var term = query.Query.Trim();
            var pattern = $"%{term}%";
            skus = skus
                .Where(sku => EF.Functions.ILike(sku.SkuCode, pattern) || EF.Functions.ILike(sku.Name, pattern)
                    || sku.Barcodes.Any(barcode => EF.Functions.ILike(barcode, pattern)))
                // Mã quét từ máy quét khớp đúng một SKU: SKU đó phải nằm trong các dòng đầu.
                .OrderByDescending(sku => EF.Functions.ILike(sku.SkuCode, term) || sku.Barcodes.Contains(term))
                .ThenBy(sku => sku.SkuCode);
        }

        // SKU chưa có dòng số dư ở chi nhánh thì tồn khả dụng bằng 0.
        return await skus
            .Take(limit)
            .Select(sku => new PosSkuDto(
                sku.SkuId, sku.SkuCode, sku.Name, sku.Barcodes, sku.RetailPrice,
                db.InventoryBalances
                    .Where(balance => balance.BranchId == query.BranchId && balance.SkuId == sku.SkuId)
                    .Select(balance => balance.OnHand - balance.Reserved)
                    .FirstOrDefault()))
            .ToListAsync(ct);
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
