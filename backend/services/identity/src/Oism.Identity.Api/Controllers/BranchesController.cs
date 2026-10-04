using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oism.BuildingBlocks.Auth;
using Oism.Identity.Application.Branches;
using Oism.Identity.Application.Branches.ListBranches;
using Oism.Identity.Application.Branches.SetBranchActive;
using Oism.Identity.Application.Branches.UpsertBranch;

namespace Oism.Identity.Api.Controllers;

public sealed record CreateBranchRequest(string Code, string Name, string Type, string? Address);

public sealed record UpdateBranchRequest(string Name, string Type, string? Address);

public sealed record SetBranchActiveRequest(bool IsActive);

[ApiController]
[Route("branches")]
public sealed class BranchesController(
    ListBranchesHandler listBranches, UpsertBranchHandler upsertBranch, SetBranchActiveHandler setBranchActive)
    : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<IReadOnlyList<BranchDto>>> List([FromQuery] bool? isActive, CancellationToken ct) =>
        Ok(await listBranches.Handle(isActive, ct));

    [HttpPost]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<BranchDto>> Create(CreateBranchRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await upsertBranch.Handle(
            new UpsertBranchCommand(null, request.Code, request.Name, request.Type, request.Address), ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<BranchDto>> Update(Guid id, UpdateBranchRequest request, CancellationToken ct) =>
        Ok(await upsertBranch.Handle(new UpsertBranchCommand(id, null, request.Name, request.Type, request.Address), ct));

    [HttpPatch("{id:guid}/active")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<BranchDto>> SetActive(Guid id, SetBranchActiveRequest request, CancellationToken ct) =>
        Ok(await setBranchActive.Handle(new SetBranchActiveCommand(id, request.IsActive), ct));
}
