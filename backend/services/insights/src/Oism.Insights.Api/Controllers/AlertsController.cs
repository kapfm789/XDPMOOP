using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oism.BuildingBlocks.Auth;
using Oism.Insights.Application.Alerts.ListLowStock;

namespace Oism.Insights.Api.Controllers;

// UC-REP-03: danh sách cảnh báo tồn thấp cho Owner và Staff (docs/architecture/security.md).
// Thông báo đẩy tức thời đi qua SignalR, không qua controller này.
[ApiController]
[Route("alerts")]
public sealed class AlertsController(ListLowStockHandler listLowStock) : ControllerBase
{
    [HttpGet("low-stock")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<IReadOnlyList<LowStockAlertRow>>> ListLowStock(
        CancellationToken ct, [FromQuery] Guid? branchId = null) =>
        Ok(await listLowStock.Handle(new ListLowStockQuery(branchId), ct));
}
