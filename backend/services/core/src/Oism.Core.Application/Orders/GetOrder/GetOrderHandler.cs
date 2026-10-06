using Oism.SharedKernel;

namespace Oism.Core.Application.Orders.GetOrder;

public sealed record GetOrderQuery(Guid OrderId, bool IncludeCost);

// UC-ORD-06: đơn kèm dòng đơn, phần giữ hàng và thanh toán, để xem chi tiết và in phiếu giao.
public sealed class GetOrderHandler(IOrderQueries queries)
{
    public async Task<OrderDto> Handle(GetOrderQuery query, CancellationToken ct) =>
        await queries.FindAsync(query.OrderId, query.IncludeCost, ct) ?? throw new NotFoundException("đơn hàng");
}
