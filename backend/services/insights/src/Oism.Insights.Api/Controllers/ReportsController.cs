using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oism.BuildingBlocks.Auth;
using Oism.Insights.Application.Reports.GetGrossProfit;

namespace Oism.Insights.Api.Controllers;

// UC-REP-01: báo cáo lợi nhuận gộp chỉ Owner xem (docs/architecture/security.md).
[ApiController]
[Route("reports")]
public sealed class ReportsController(GetGrossProfitHandler getGrossProfit) : ControllerBase
{
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
}
