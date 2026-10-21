using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oism.BuildingBlocks.Auth;
using Oism.Identity.Application.Branches;
using Oism.Identity.Application.Branches.GetBranch;
using Oism.Identity.Application.Branches.ListBranches;
using Oism.Identity.Application.Branches.SetBranchActive;
using Oism.Identity.Application.Branches.UpsertBranch;

namespace Oism.Identity.Api.Controllers;

public sealed record CreateBranchRequest(
    string Code, string Name, string Type, string? Address, string? BankBin = null, string? BankAccount = null);

public sealed record UpdateBranchRequest(string Name, string Type, string? Address, string? BankBin = null, string? BankAccount = null);

public sealed record SetBranchActiveRequest(bool IsActive);

[ApiController]
[Route("branches")]
public sealed class BranchesController(
    ListBranchesHandler listBranches, GetBranchHandler getBranch, UpsertBranchHandler upsertBranch,
    SetBranchActiveHandler setBranchActive)
    : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<IReadOnlyList<BranchDto>>> List([FromQuery] bool? isActive, CancellationToken ct) =>
        Ok(await listBranches.Handle(isActive, ct));

    // Cashier gắn với một chi nhánh qua claim `branch_id` và chỉ đọc được chi nhánh đó (docs/design/api/identity.md).
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.AnyRole)]
    public async Task<ActionResult<BranchDto>> Get(Guid id, CancellationToken ct)
    {
        if (User.IsInRole(Roles.Cashier) && User.FindFirstValue("branch_id") != id.ToString())
            return Forbid();

        return Ok(await getBranch.Handle(id, ct));
    }

    [HttpPost]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<BranchDto>> Create(CreateBranchRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await upsertBranch.Handle(
            new UpsertBranchCommand(
                null, request.Code, request.Name, request.Type, request.Address, request.BankBin, request.BankAccount), ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<BranchDto>> Update(Guid id, UpdateBranchRequest request, CancellationToken ct) =>
        Ok(await upsertBranch.Handle(
            new UpsertBranchCommand(id, null, request.Name, request.Type, request.Address, request.BankBin, request.BankAccount), ct));

    [HttpPatch("{id:guid}/active")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<BranchDto>> SetActive(Guid id, SetBranchActiveRequest request, CancellationToken ct) =>
        Ok(await setBranchActive.Handle(new SetBranchActiveCommand(id, request.IsActive), ct));
}
