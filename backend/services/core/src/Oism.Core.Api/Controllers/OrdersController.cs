using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oism.BuildingBlocks.Auth;
using Oism.Core.Application.Orders;
using Oism.Core.Application.Orders.ReserveStock;
using Oism.Core.Domain.Orders;

namespace Oism.Core.Api.Controllers;

public sealed record CreateOrderRequest(Guid BranchId, string? Note, IReadOnlyList<ReserveStockLine>? Items);

[ApiController]
[Route("orders")]
public sealed class OrdersController(ReserveStockHandler reserveStock) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);

    // UC-ORD-02: đơn thủ công luôn mang kênh Admin.
    [HttpPost]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await reserveStock.Handle(
            new ReserveStockCommand(request.BranchId, OrderChannel.Admin, request.Note, request.Items ?? [], UserId), ct));
}
