using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oism.BuildingBlocks.Auth;
using Oism.Channel.Application;
using Oism.Channel.Application.Simulator.SimulateBurst;
using Oism.Channel.Application.Webhooks;
using Oism.Channel.Application.Webhooks.ListWebhookEvents;

namespace Oism.Channel.Api.Controllers;

public sealed record SimulateBurstRequest(
    string? Channel, string? ShopId, string? SkuCode, int QuantityPerOrder, int Orders, int Concurrency, decimal? UnitPrice);

// UC-SIM-01: công cụ bắn tải và nhật ký webhook đã nhận, chỉ Owner (docs/architecture/security.md).
[ApiController]
public sealed class SimulatorController(SimulateBurstHandler simulateBurst, ListWebhookEventsHandler listWebhookEvents) : ControllerBase
{
    // Trả về sau khi mọi webhook đã được gửi; kết quả giữ hàng của từng đơn xem ở GET /webhook-events.
    [HttpPost("simulator/burst")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<BurstResult>> Burst(SimulateBurstRequest request, CancellationToken ct) =>
        Accepted(await simulateBurst.Handle(
            new SimulateBurstCommand(
                request.Channel, request.ShopId, request.SkuCode, request.QuantityPerOrder, request.Orders, request.Concurrency,
                request.UnitPrice),
            ct));

    [HttpGet("webhook-events")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<PagedResult<WebhookEventDto>>> ListWebhookEvents(
        CancellationToken ct,
        [FromQuery] string? channel = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        Ok(await listWebhookEvents.Handle(new ListWebhookEventsQuery(channel, status, page, pageSize), ct));
}
