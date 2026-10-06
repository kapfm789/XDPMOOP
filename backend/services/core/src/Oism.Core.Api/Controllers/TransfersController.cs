using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oism.BuildingBlocks.Auth;
using Oism.Core.Application;
using Oism.Core.Application.Inventory;
using Oism.Core.Application.Inventory.CreateTransfer;
using Oism.Core.Application.Inventory.DeleteTransfer;
using Oism.Core.Application.Inventory.ListTransfers;
using Oism.Core.Application.Inventory.ReceiveTransfer;
using Oism.Core.Application.Inventory.ShipTransfer;
using Oism.Core.Domain.Inventory;

namespace Oism.Core.Api.Controllers;

public sealed record CreateTransferRequest(Guid FromBranchId, Guid ToBranchId, IReadOnlyList<TransferLine>? Items);

// UC-INV-03: chuyển kho giữa hai chi nhánh qua hai bước xuất và nhận.
[ApiController]
[Route("transfers")]
public sealed class TransfersController(
    ListTransfersHandler listTransfers,
    CreateTransferHandler createTransfer,
    DeleteTransferHandler deleteTransfer,
    ShipTransferHandler shipTransfer,
    ReceiveTransferHandler receiveTransfer) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);

    // Giá vốn chỉ trả cho Owner: docs/architecture/security.md.
    private bool CanSeeCost => User.IsInRole(Roles.Owner);

    [HttpGet]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<PagedResult<TransferDto>>> List(
        CancellationToken ct,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        Ok(await listTransfers.Handle(new ListTransfersQuery(status, page, pageSize, CanSeeCost), ct));

    [HttpPost]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<TransferDto>> Create(CreateTransferRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await createTransfer.Handle(
            new CreateTransferCommand(request.FromBranchId, request.ToBranchId, request.Items ?? [], UserId, CanSeeCost), ct));

    [HttpPost("{id:guid}/ship")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<TransferDto>> Ship(Guid id, CancellationToken ct) =>
        Ok(await shipTransfer.Handle(new ShipTransferCommand(id, UserId, CanSeeCost), ct));

    // Chỉ phiếu còn Draft xóa được (ADR-0009).
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await deleteTransfer.Handle(new DeleteTransferCommand(id), ct);
        return NoContent();
    }

    // Nhận lại phiếu đã Received trả 200 với cùng phiếu, không tác động lần hai.
    [HttpPost("{id:guid}/receive")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<TransferDto>> Receive(Guid id, CancellationToken ct) =>
        Ok(await receiveTransfer.Handle(new ReceiveTransferCommand(id, UserId, CanSeeCost), ct));
}
