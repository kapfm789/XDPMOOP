using Oism.BuildingBlocks.Messaging;
using Oism.Contracts;
using Oism.Core.Application.Orders.ReserveStock;

namespace Oism.Core.Api.Consumers;

// UC-ORD-01: đơn online từ `channel` đi vào cùng handler với đơn thủ công (docs/design/flows/reserve-stock.md).
public sealed class SubmitOrderConsumer(ReserveStockHandler reserveStock) : EventConsumer<SubmitOrder>
{
    public override Task HandleAsync(SubmitOrder payload, CancellationToken ct) => reserveStock.Handle(payload, ct);
}
