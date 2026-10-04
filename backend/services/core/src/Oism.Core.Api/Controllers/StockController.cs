using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oism.BuildingBlocks.Auth;
using Oism.Core.Application;
using Oism.Core.Application.Inventory;
using Oism.Core.Application.Inventory.ListLedger;
using Oism.Core.Application.Inventory.ListStock;
using Oism.Core.Application.Inventory.SetThreshold;

namespace Oism.Core.Api.Controllers;

public sealed record SetThresholdRequest(Guid BranchId, Guid SkuId, int? Threshold);

// UC-INV-01: xem tồn và sổ giao dịch. Sổ chỉ có đường đọc. UC-INV-05: đặt ngưỡng tồn.
[ApiController]
public sealed class StockController(ListStockHandler listStock, ListLedgerHandler listLedger, SetThresholdHandler setThreshold)
    : ControllerBase
{
    // Giá vốn chỉ trả cho Owner: docs/architecture/security.md.
    private bool CanSeeCost => User.IsInRole(Roles.Owner);

    [HttpGet("stock")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<PagedResult<StockDto>>> ListStock(
        CancellationToken ct,
        [FromQuery] Guid? branchId = null,
        [FromQuery] Guid? skuId = null,
        [FromQuery] string? query = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        Ok(await listStock.Handle(new ListStockQuery(branchId, skuId, query, page, pageSize, CanSeeCost), ct));

    [HttpPut("stock/threshold")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<StockDto>> SetThreshold(SetThresholdRequest request, CancellationToken ct) =>
        Ok(await setThreshold.Handle(new SetThresholdCommand(request.BranchId, request.SkuId, request.Threshold, CanSeeCost), ct));

    [HttpGet("ledger")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<PagedResult<LedgerLineDto>>> ListLedger(
        CancellationToken ct,
        [FromQuery] Guid? branchId = null,
        [FromQuery] Guid? skuId = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        Ok(await listLedger.Handle(new ListLedgerQuery(branchId, skuId, from, to, page, pageSize, CanSeeCost), ct));
}
