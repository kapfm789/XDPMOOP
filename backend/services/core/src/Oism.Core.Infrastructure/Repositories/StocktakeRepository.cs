using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Tenancy;
using Oism.Core.Application.Inventory;
using Oism.Core.Domain.Inventory;
using Oism.Core.Domain.Orders;

namespace Oism.Core.Infrastructure.Repositories;

internal sealed class StocktakeRepository(CoreDbContext db, ITenantContext tenant) : IStocktakeRepository
{
    public async Task<Stocktake?> GetForUpdateAsync(Guid id, CancellationToken ct)
    {
        // Khóa dòng chỉ giữ tới hết transaction; khóa ngoài transaction thì nhả ngay sau câu lệnh.
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("A stocktake must be locked inside a transaction.");

        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("Cannot lock a stocktake without a tenant context.");

        // Điều kiện tenant_id nằm ngay trong câu khóa để chỉ khóa dòng của tenant hiện tại;
        // Global Query Filter vẫn được áp ở lớp ngoài.
        var stocktake = (await db.Stocktakes
            .FromSql($"SELECT * FROM core.stocktakes WHERE tenant_id = {tenantId} AND id = {id} FOR UPDATE")
            .ToListAsync(ct)).SingleOrDefault();
        if (stocktake is not null)
            await db.Entry(stocktake).Collection(nameof(Stocktake.Items)).LoadAsync(ct);
        return stocktake;
    }

    public void Add(Stocktake stocktake) => db.Add(stocktake);

    public async Task<ILookup<Guid, HeldOrder>> ListHeldOrdersAsync(
        Guid branchId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct)
    {
        var holds = await (
            from hold in db.Reservations.AsNoTracking()
            join order in db.Orders.AsNoTracking() on hold.OrderId equals order.Id
            where hold.BranchId == branchId && hold.Status == ReservationStatus.Active && skuIds.Contains(hold.SkuId)
            orderby order.CreatedAt, order.Id
            select new { hold.SkuId, order.Id, order.OrderNumber, order.Channel, hold.Quantity }).ToListAsync(ct);

        // Một đơn có hai dòng cùng SKU thì có hai phần giữ: gộp lại thành một dòng cho đơn đó.
        return holds
            .GroupBy(hold => new { hold.SkuId, hold.Id, hold.OrderNumber, hold.Channel })
            .ToLookup(
                group => group.Key.SkuId,
                group => new HeldOrder(group.Key.Id, group.Key.OrderNumber, group.Key.Channel.ToString(), group.Sum(hold => hold.Quantity)));
    }
}
