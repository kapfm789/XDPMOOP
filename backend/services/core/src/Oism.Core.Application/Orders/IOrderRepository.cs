using Oism.Core.Domain.Orders;

namespace Oism.Core.Application.Orders;

public interface IOrderRepository
{
    void Add(Order order);
}
