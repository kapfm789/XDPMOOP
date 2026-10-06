using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oism.BuildingBlocks.Auth;
using Oism.Core.Application;
using Oism.Core.Application.Orders;
using Oism.Core.Application.Orders.CancelOrder;
using Oism.Core.Application.Orders.CompleteOrder;
using Oism.Core.Application.Orders.ConfirmOrder;
using Oism.Core.Application.Orders.GetOrder;
using Oism.Core.Application.Orders.ListOrders;
using Oism.Core.Application.Orders.ReserveStock;
using Oism.Core.Domain.Orders;

namespace Oism.Core.Api.Controllers;

public sealed record CreateOrderRequest(Guid BranchId, string? Note, IReadOnlyList<ReserveStockLine>? Items);

[ApiController]
[Route("orders")]
public sealed class OrdersController(
    ListOrdersHandler listOrders,
    GetOrderHandler getOrder,
    ReserveStockHandler reserveStock,
    ConfirmOrderHandler confirmOrder,
    CompleteOrderHandler completeOrder,
    CancelOrderHandler cancelOrder) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);

    // Giá vốn chỉ trả cho Owner: docs/architecture/security.md.
    private bool CanSeeCost => User.IsInRole(Roles.Owner);

    // UC-ORD-06 AC-4: `from` và `to` lọc theo thời điểm tạo đơn, tính cả hai đầu.
    [HttpGet]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<PagedResult<OrderDto>>> List(
        CancellationToken ct,
        [FromQuery] string? status = null,
        [FromQuery] string? channel = null,
        [FromQuery] Guid? branchId = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        Ok(await listOrders.Handle(new ListOrdersQuery(status, channel, branchId, from, to, page, pageSize, CanSeeCost), ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<OrderDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await getOrder.Handle(new GetOrderQuery(id, CanSeeCost), ct));

    // UC-ORD-02: đơn thủ công luôn mang kênh Admin.
    [HttpPost]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await reserveStock.Handle(
            new ReserveStockCommand(request.BranchId, OrderChannel.Admin, request.Note, request.Items ?? [], UserId), ct));

    // UC-ORD-03.
    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<OrderDto>> Confirm(Guid id, CancellationToken ct) =>
        Ok(await confirmOrder.Handle(new ConfirmOrderCommand(id, UserId, CanSeeCost), ct));

    // UC-ORD-06.
    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<OrderDto>> Complete(Guid id, CancellationToken ct) =>
        Ok(await completeOrder.Handle(new CompleteOrderCommand(id, CanSeeCost), ct));

    // UC-ORD-04: hủy lại đơn đã Cancelled trả 200 với cùng đơn, không tác động lần hai.
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<OrderDto>> Cancel(Guid id, CancellationToken ct) =>
        Ok(await cancelOrder.Handle(new CancelOrderCommand(id, OrderCancelReason.Manual), ct));
}
