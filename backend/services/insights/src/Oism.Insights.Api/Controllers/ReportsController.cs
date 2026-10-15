using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oism.BuildingBlocks.Auth;
using Oism.Insights.Application.Reports.GetGrossProfit;
using Oism.Insights.Application.Reports.GetInventoryValue;
using Oism.Insights.Application.Reports.GetSlowMovers;
using Oism.Insights.Application.Reports.GetTopSellers;

namespace Oism.Insights.Api.Controllers;

// UC-REP-01: báo cáo lợi nhuận gộp chỉ Owner xem. UC-REP-02: giá trị tồn, bán chạy, bán chậm cho Owner và Staff
// (docs/architecture/security.md).
[ApiController]
[Route("reports")]
public sealed class ReportsController(
    GetGrossProfitHandler getGrossProfit,
    GetInventoryValueHandler getInventoryValue,
    GetTopSellersHandler getTopSellers,
    GetSlowMoversHandler getSlowMovers) : ControllerBase
{
    // Giá vốn chỉ trả cho Owner: docs/architecture/security.md.
    private bool CanSeeCost => User.IsInRole(Roles.Owner);

    [HttpGet("gross-profit")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<GrossProfitReport>> GetGrossProfit(
        CancellationToken ct,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] Guid? branchId = null,
        [FromQuery] string? channel = null,
        [FromQuery] Guid? skuId = null,
        [FromQuery] string? groupBy = null) =>
        Ok(await getGrossProfit.Handle(new GetGrossProfitQuery(from, to, branchId, channel, skuId, groupBy), ct));

    [HttpGet("inventory-value")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<InventoryValueReport>> GetInventoryValue(
        CancellationToken ct,
        [FromQuery] Guid? branchId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        Ok(await getInventoryValue.Handle(new GetInventoryValueQuery(branchId, page, pageSize, CanSeeCost), ct));

    [HttpGet("top-sellers")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<IReadOnlyList<TopSellerRow>>> GetTopSellers(
        CancellationToken ct,
        [FromQuery] int days = 30,
        [FromQuery] Guid? branchId = null,
        [FromQuery] int limit = 20) =>
        Ok(await getTopSellers.Handle(new GetTopSellersQuery(days, branchId, limit), ct));

    [HttpGet("slow-movers")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<IReadOnlyList<SlowMoverRow>>> GetSlowMovers(
        CancellationToken ct,
        [FromQuery] int days = 30,
        [FromQuery] Guid? branchId = null,
        [FromQuery] int limit = 20) =>
        Ok(await getSlowMovers.Handle(new GetSlowMoversQuery(days, branchId, limit), ct));
}
