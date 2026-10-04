using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oism.BuildingBlocks.Auth;
using Oism.Core.Application;
using Oism.Core.Application.Inventory;
using Oism.Core.Application.Inventory.ConfirmPurchaseReceipt;
using Oism.Core.Application.Inventory.ListPurchaseReceipts;
using Oism.Core.Application.Inventory.Suppliers;
using Oism.Core.Application.Inventory.UpsertPurchaseReceipt;
using Oism.Core.Domain.Inventory;

namespace Oism.Core.Api.Controllers;

public sealed record CreateSupplierRequest(string? Name, string? Phone);

public sealed record CreatePurchaseReceiptRequest(Guid BranchId, Guid SupplierId, string? Note, IReadOnlyList<PurchaseLine>? Items);

public sealed record UpdatePurchaseReceiptRequest(Guid SupplierId, string? Note, IReadOnlyList<PurchaseLine>? Items);

// UC-INV-02: nhập hàng từ nhà cung cấp.
[ApiController]
public sealed class PurchaseReceiptsController(
    ListSuppliersHandler listSuppliers,
    CreateSupplierHandler createSupplier,
    ListPurchaseReceiptsHandler listReceipts,
    UpsertPurchaseReceiptHandler upsertReceipt,
    ConfirmPurchaseReceiptHandler confirmReceipt) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);

    [HttpGet("suppliers")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<IReadOnlyList<SupplierDto>>> ListSuppliers(CancellationToken ct) =>
        Ok(await listSuppliers.Handle(ct));

    [HttpPost("suppliers")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<SupplierDto>> CreateSupplier(CreateSupplierRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await createSupplier.Handle(
            new CreateSupplierCommand(request.Name ?? string.Empty, request.Phone), ct));

    [HttpGet("purchase-receipts")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<PagedResult<PurchaseReceiptDto>>> List(
        CancellationToken ct,
        [FromQuery] Guid? branchId = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        Ok(await listReceipts.Handle(new ListPurchaseReceiptsQuery(branchId, status, page, pageSize), ct));

    [HttpPost("purchase-receipts")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<PurchaseReceiptDto>> Create(CreatePurchaseReceiptRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await upsertReceipt.Handle(
            new UpsertPurchaseReceiptCommand(null, request.BranchId, request.SupplierId, request.Note, request.Items ?? []), ct));

    [HttpPut("purchase-receipts/{id:guid}")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<PurchaseReceiptDto>> Update(Guid id, UpdatePurchaseReceiptRequest request, CancellationToken ct) =>
        Ok(await upsertReceipt.Handle(
            new UpsertPurchaseReceiptCommand(id, Guid.Empty, request.SupplierId, request.Note, request.Items ?? []), ct));

    // Xác nhận lại phiếu đã Confirmed trả 200 với cùng phiếu, không tác động lần hai.
    [HttpPost("purchase-receipts/{id:guid}/confirm")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<PurchaseReceiptDto>> Confirm(Guid id, CancellationToken ct) =>
        Ok(await confirmReceipt.Handle(new ConfirmPurchaseReceiptCommand(id, UserId), ct));
}
