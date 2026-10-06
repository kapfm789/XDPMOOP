using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Tenancy;
using Oism.Core.Application.Inventory;
using Oism.Core.Domain.Inventory;

namespace Oism.Core.Infrastructure.Repositories;

internal sealed class StockTransferRepository(CoreDbContext db, ITenantContext tenant) : IStockTransferRepository
{
    public async Task<StockTransfer?> GetForUpdateAsync(Guid id, CancellationToken ct)
    {
        // Khóa dòng chỉ giữ tới hết transaction; khóa ngoài transaction thì nhả ngay sau câu lệnh.
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("A stock transfer must be locked inside a transaction.");

        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("Cannot lock a stock transfer without a tenant context.");

        // Điều kiện tenant_id nằm ngay trong câu khóa để chỉ khóa dòng của tenant hiện tại;
        // Global Query Filter vẫn được áp ở lớp ngoài.
        var transfer = (await db.StockTransfers
            .FromSql($"SELECT * FROM core.stock_transfers WHERE tenant_id = {tenantId} AND id = {id} FOR UPDATE")
            .ToListAsync(ct)).SingleOrDefault();
        if (transfer is not null)
            await db.Entry(transfer).Collection(nameof(StockTransfer.Items)).LoadAsync(ct);
        return transfer;
    }

    public void Add(StockTransfer transfer) => db.Add(transfer);

    // Các dòng đã nạp cùng phiếu nên bị xóa theo phiếu.
    public void Remove(StockTransfer transfer) => db.Remove(transfer);
}
