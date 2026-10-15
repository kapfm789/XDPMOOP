using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oism.BuildingBlocks.Auth;
using Oism.Insights.Application.Forecasting.GetReorderSuggestions;
using Oism.Insights.Application.Forecasting.RunForecast;

namespace Oism.Insights.Api.Controllers;

public sealed record ForecastRunAccepted(Guid RunId);

// UC-AI-01, UC-AI-02: đề xuất nhập hàng và chạy dự báo, chỉ Owner (docs/architecture/security.md).
[ApiController]
[Route("forecast")]
public sealed class ForecastController(
    GetReorderSuggestionsHandler getReorderSuggestions, RunForecastHandler runForecast) : ControllerBase
{
    [HttpGet("reorder-suggestions")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<ReorderSuggestions>> GetReorderSuggestions(
        CancellationToken ct,
        [FromQuery] Guid? branchId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        Ok(await getReorderSuggestions.Handle(new GetReorderSuggestionsQuery(branchId, page, pageSize), ct));

    // Trả 202 khi lần chạy đã được ghi lại, kể cả lần chạy lỗi; trạng thái xem ở reorder-suggestions.
    [HttpPost("run")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<ForecastRunAccepted>> Run(CancellationToken ct) =>
        Accepted(new ForecastRunAccepted(await runForecast.Handle(new RunForecastCommand(), ct)));
}
