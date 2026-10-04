using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oism.BuildingBlocks.Auth;
using Oism.Catalog.Application.Brands;
using Oism.Catalog.Application.Brands.ListBrands;
using Oism.Catalog.Application.Brands.UpsertBrand;

namespace Oism.Catalog.Api.Controllers;

public sealed record BrandRequest(string Name);

[ApiController]
[Route("brands")]
public sealed class BrandsController(ListBrandsHandler listBrands, UpsertBrandHandler upsertBrand) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<IReadOnlyList<BrandDto>>> List(CancellationToken ct) =>
        Ok(await listBrands.Handle(ct));

    [HttpPost]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<BrandDto>> Create(BrandRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await upsertBrand.Handle(new UpsertBrandCommand(null, request.Name), ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<BrandDto>> Update(Guid id, BrandRequest request, CancellationToken ct) =>
        Ok(await upsertBrand.Handle(new UpsertBrandCommand(id, request.Name), ct));
}
