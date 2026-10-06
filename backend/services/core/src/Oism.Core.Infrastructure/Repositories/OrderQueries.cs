using Microsoft.EntityFrameworkCore;
using Oism.Core.Application;
using Oism.Core.Application.Orders;
using Oism.Core.Application.Orders.ListOrders;
using Oism.Core.Domain.Orders;

namespace Oism.Core.Infrastructure.Repositories;

// Chỉ đọc.
internal sealed class OrderQueries(CoreDbContext db) : IOrderQueries
{
    public async Task<PagedResult<OrderDto>> ListAsync(ListOrdersQuery query, CancellationToken ct)
    {
        var status = query.Status is null ? (OrderStatus?)null : Enum.Parse<OrderStatus>(query.Status);
        var channel = query.Channel is null ? (OrderChannel?)null : Enum.Parse<OrderChannel>(query.Channel);
        // Npgsql chỉ nhận thời điểm ở UTC cho cột timestamptz.
        var (from, to) = (query.From?.ToUniversalTime(), query.To?.ToUniversalTime());
        var orders = db.Orders.AsNoTracking().Where(order =>
            (status == null || order.Status == status)
            && (channel == null || order.Channel == channel)
            && (query.BranchId == null || order.BranchId == query.BranchId)
            && (from == null || order.CreatedAt >= from)
            && (to == null || order.CreatedAt <= to));

        var page = await orders
            .OrderByDescending(order => order.CreatedAt).ThenBy(order => order.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Include(order => order.Items).Include(order => order.Payment)
            .ToListAsync(ct);
        return new PagedResult<OrderDto>(
            page.Select(order => OrderDto.From(order, query.IncludeCost)).ToList(), query.Page, query.PageSize, await orders.CountAsync(ct));
    }

    public async Task<OrderDto?> FindAsync(Guid id, bool includeCost, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(order => order.Items).Include(order => order.Payment)
            .SingleOrDefaultAsync(order => order.Id == id, ct);
        if (order is null)
            return null;

        var holds = await db.Reservations.AsNoTracking()
            .Where(hold => hold.OrderId == id)
            .OrderBy(hold => hold.SkuId).ThenBy(hold => hold.Id)
            .ToListAsync(ct);
        return OrderDto.From(order, includeCost, holds);
    }
}
