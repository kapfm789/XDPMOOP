using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Tenancy;
using Oism.Core.Application.Orders;
using Oism.Core.Domain.Orders;

namespace Oism.Core.Infrastructure.Repositories;

internal sealed class OrderRepository(CoreDbContext db, ITenantContext tenant) : IOrderRepository
{
    public async Task<Order?> GetForUpdateAsync(Guid id, CancellationToken ct)
    {
        // Khóa dòng chỉ giữ tới hết transaction; khóa ngoài transaction thì nhả ngay sau câu lệnh.
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("An order must be locked inside a transaction.");

        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("Cannot lock an order without a tenant context.");

        // Điều kiện tenant_id nằm ngay trong câu khóa để chỉ khóa dòng của tenant hiện tại;
        // Global Query Filter vẫn được áp ở lớp ngoài.
        var order = (await db.Orders
            .FromSql($"SELECT * FROM core.orders WHERE tenant_id = {tenantId} AND id = {id} FOR UPDATE")
            .ToListAsync(ct)).SingleOrDefault();
        if (order is not null)
            await db.Entry(order).Collection(nameof(Order.Items)).LoadAsync(ct);
        return order;
    }

    public Task<Order?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) =>
        db.Orders.AsNoTracking()
            .Include(order => order.Items).Include(order => order.Payment)
            .SingleOrDefaultAsync(order => order.IdempotencyKey == idempotencyKey, ct);

    public void Add(Order order) => db.Add(order);
}
