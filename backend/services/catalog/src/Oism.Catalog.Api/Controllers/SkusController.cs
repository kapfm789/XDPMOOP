using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oism.BuildingBlocks.Auth;
using Oism.Catalog.Application.Products;
using Oism.Catalog.Application.Products.AddBarcode;
using Oism.Catalog.Application.Products.RemoveBarcode;
using Oism.Catalog.Application.Products.SetPrices;
using Oism.Catalog.Application.Products.UpdateSku;

namespace Oism.Catalog.Api.Controllers;

public sealed record UpdateSkuRequest(IReadOnlyDictionary<string, string>? Attributes, bool IsActive);

public sealed record AddBarcodeRequest(string Symbology, string? Code);

public sealed record SetPricesRequest(decimal RetailPrice, decimal WholesalePrice);

[ApiController]
[Route("skus")]
public sealed class SkusController(
    UpdateSkuHandler updateSku, AddBarcodeHandler addBarcode, RemoveBarcodeHandler removeBarcode, SetPricesHandler setPrices)
    : ControllerBase
{
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<SkuDto>> Update(Guid id, UpdateSkuRequest request, CancellationToken ct) =>
        Ok(await updateSku.Handle(new UpdateSkuCommand(id, request.Attributes, request.IsActive), ct));

    [HttpPost("{id:guid}/barcodes")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<BarcodeDto>> AddBarcode(Guid id, AddBarcodeRequest request, CancellationToken ct) =>
        StatusCode(
            StatusCodes.Status201Created,
            await addBarcode.Handle(new AddBarcodeCommand(id, request.Symbology, request.Code), ct));

    [HttpDelete("{id:guid}/barcodes/{barcodeId:guid}")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<IActionResult> RemoveBarcode(Guid id, Guid barcodeId, CancellationToken ct)
    {
        await removeBarcode.Handle(new RemoveBarcodeCommand(id, barcodeId), ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/prices")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<SkuDto>> SetPrices(Guid id, SetPricesRequest request, CancellationToken ct) =>
        Ok(await setPrices.Handle(new SetPricesCommand(id, request.RetailPrice, request.WholesalePrice), ct));
}
