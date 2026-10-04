using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Tenancy;
using Oism.Core.Application.Inventory;
using Oism.Core.Domain.Inventory;

namespace Oism.Core.Infrastructure.Repositories;

internal sealed class InventoryRepository(CoreDbContext db, ITenantContext tenant) : IInventoryRepository
{
    // Hai câu lệnh theo docs/architecture/transactions-and-concurrency.md mục "Thứ tự khóa".
    // Số dư chỉ được nạp có theo dõi qua đây, mỗi transaction một DbContext: entity nạp trước khi khóa sẽ mang giá trị cũ.
    public async Task<IReadOnlyDictionary<Guid, InventoryBalance>> LockBalancesAsync(
        Guid branchId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct)
    {
        // Khóa dòng chỉ giữ tới hết transaction; khóa ngoài transaction thì nhả ngay sau câu lệnh.
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Inventory balances must be locked inside a transaction.");

        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("Cannot lock inventory balances without a tenant context.");
        // Cùng một thứ tự cho mọi transaction, ở cả bước tạo dòng lẫn bước khóa.
        var ids = skuIds.Distinct().Order().ToArray();

        // Tạo dòng số dư nếu SKU chưa từng có ở chi nhánh, để lúc khóa không bị hụt.
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO core.inventory_balances (tenant_id, branch_id, sku_id, on_hand, reserved, avg_cost, version, updated_at)
            SELECT {tenantId}, {branchId}, sku_id, 0, 0, 0, 0, now()
            FROM unnest({ids}) AS sku_id
            ON CONFLICT (tenant_id, branch_id, sku_id) DO NOTHING
            """,
            ct);

        // Điều kiện tenant_id nằm ngay trong câu khóa để chỉ khóa dòng của tenant hiện tại;
        // Global Query Filter vẫn được áp ở lớp ngoài.
        var balances = await db.InventoryBalances
            .FromSql(
                $"""
                SELECT * FROM core.inventory_balances
                WHERE tenant_id = {tenantId} AND branch_id = {branchId} AND sku_id = ANY({ids})
                ORDER BY branch_id, sku_id
                FOR UPDATE
                """)
            .ToListAsync(ct);
        return balances.ToDictionary(balance => balance.SkuId);
    }

    public void Add(InventoryTransaction entry) => db.Add(entry);
}
