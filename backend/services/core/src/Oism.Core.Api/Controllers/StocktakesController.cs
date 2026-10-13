using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oism.BuildingBlocks.Auth;
using Oism.Core.Application;
using Oism.Core.Application.Inventory;
using Oism.Core.Application.Inventory.ListStocktakes;
using Oism.Core.Application.Inventory.OpenStocktake;
using Oism.Core.Application.Inventory.PostStocktake;
using Oism.Core.Application.Inventory.SetStocktakeCounts;
using Oism.Core.Domain.Inventory;

namespace Oism.Core.Api.Controllers;

public sealed record OpenStocktakeRequest(Guid BranchId);

public sealed record SetStocktakeCountsRequest(IReadOnlyList<StocktakeCount>? Items);

// UC-INV-04: kiểm kê qua ba bước mở phiên, nhập số đếm, chốt.
[ApiController]
[Route("stocktakes")]
public sealed class StocktakesController(
    ListStocktakesHandler listStocktakes,
    OpenStocktakeHandler openStocktake,
    SetStocktakeCountsHandler setCounts,
    PostStocktakeHandler postStocktake) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);

    [HttpGet]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<PagedResult<StocktakeDto>>> List(
        CancellationToken ct,
        [FromQuery] Guid? branchId = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        Ok(await listStocktakes.Handle(new ListStocktakesQuery(branchId, status, page, pageSize), ct));

    [HttpPost]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<StocktakeDto>> Open(OpenStocktakeRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await openStocktake.Handle(new OpenStocktakeCommand(request.BranchId, UserId), ct));

    [HttpPut("{id:guid}/counts")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<StocktakeDto>> SetCounts(Guid id, SetStocktakeCountsRequest request, CancellationToken ct) =>
        Ok(await setCounts.Handle(new SetStocktakeCountsCommand(id, request.Items ?? []), ct));

    // Chốt lại phiên đã Posted trả 200 với cùng phiên, không tác động lần hai.
    [HttpPost("{id:guid}/post")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<StocktakeDto>> Post(Guid id, CancellationToken ct) =>
        Ok(await postStocktake.Handle(new PostStocktakeCommand(id, UserId), ct));
}
