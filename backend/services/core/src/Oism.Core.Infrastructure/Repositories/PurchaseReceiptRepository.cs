using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Tenancy;
using Oism.Core.Application.Inventory;
using Oism.Core.Domain.Inventory;

namespace Oism.Core.Infrastructure.Repositories;

internal sealed class PurchaseReceiptRepository(CoreDbContext db, ITenantContext tenant) : IPurchaseReceiptRepository
{
    public async Task<PurchaseReceipt?> GetForUpdateAsync(Guid id, CancellationToken ct)
    {
        // Khóa dòng chỉ giữ tới hết transaction; khóa ngoài transaction thì nhả ngay sau câu lệnh.
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("A purchase receipt must be locked inside a transaction.");

        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("Cannot lock a purchase receipt without a tenant context.");

        // Điều kiện tenant_id nằm ngay trong câu khóa để chỉ khóa dòng của tenant hiện tại;
        // Global Query Filter vẫn được áp ở lớp ngoài.
        var receipt = (await db.PurchaseReceipts
            .FromSql($"SELECT * FROM core.purchase_receipts WHERE tenant_id = {tenantId} AND id = {id} FOR UPDATE")
            .ToListAsync(ct)).SingleOrDefault();
        if (receipt is not null)
            await db.Entry(receipt).Collection(nameof(PurchaseReceipt.Items)).LoadAsync(ct);
        return receipt;
    }

    public void Add(PurchaseReceipt receipt) => db.Add(receipt);

    public Task<bool> SupplierExistsAsync(Guid supplierId, CancellationToken ct) =>
        db.Suppliers.AnyAsync(supplier => supplier.Id == supplierId, ct);

    public void Add(Supplier supplier) => db.Add(supplier);
}
