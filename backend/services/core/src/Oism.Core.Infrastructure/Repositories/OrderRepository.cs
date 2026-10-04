using Oism.Core.Application.Orders;
using Oism.Core.Domain.Orders;

namespace Oism.Core.Infrastructure.Repositories;

internal sealed class OrderRepository(CoreDbContext db) : IOrderRepository
{
    public void Add(Order order) => db.Add(order);
}
