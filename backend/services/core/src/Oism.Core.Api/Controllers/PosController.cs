using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oism.BuildingBlocks.Auth;
using Oism.Core.Application.Orders;
using Oism.Core.Application.Orders.PosCheckout;
using Oism.Core.Application.Orders.ReserveStock;
using Oism.Core.Application.Orders.SearchPosSkus;

namespace Oism.Core.Api.Controllers;

public sealed record PosCheckoutRequest(Guid BranchId, IReadOnlyList<ReserveStockLine>? Items, PosPayment? Payment);

// UC-POS-01, UC-POS-02: tìm SKU kèm tồn khả dụng và thanh toán tại quầy.
[ApiController]
[Route("pos")]
public sealed class PosController(SearchPosSkusHandler searchSkus, PosCheckoutHandler checkout) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);

    [HttpGet("skus")]
    [Authorize(Policy = Policies.AnyRole)]
    public async Task<ActionResult<IReadOnlyList<PosSkuDto>>> SearchSkus(
        CancellationToken ct, [FromQuery] Guid branchId, [FromQuery] string? query = null)
    {
        if (IsOutsideOwnBranch(branchId))
            return Forbid();

        return Ok(await searchSkus.Handle(new SearchPosSkusQuery(branchId, query), ct));
    }

    // Gửi lại cùng Idempotency-Key trả 200 với đơn đã tạo; lần đầu trả 201.
    [HttpPost("checkout")]
    [Authorize(Policy = Policies.OwnerOrCashier)]
    public async Task<ActionResult<OrderDto>> Checkout(
        PosCheckoutRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        // Trả 403 trước khi mở transaction (docs/design/flows/pos-checkout.md).
        if (IsOutsideOwnBranch(request.BranchId))
            return Forbid();

        var result = await checkout.Handle(
            new PosCheckoutCommand(
                idempotencyKey, request.BranchId, request.Items ?? [], request.Payment, UserId, User.IsInRole(Roles.Owner)),
            ct);
        return StatusCode(result.Created ? StatusCodes.Status201Created : StatusCodes.Status200OK, result.Order);
    }

    // Cashier gắn với một chi nhánh qua claim `branch_id` và chỉ bán được ở chi nhánh đó (docs/architecture/security.md).
    private bool IsOutsideOwnBranch(Guid branchId) =>
        User.IsInRole(Roles.Cashier) && User.FindFirstValue("branch_id") != branchId.ToString();
}
