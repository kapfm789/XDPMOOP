using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oism.BuildingBlocks.Auth;
using Oism.Catalog.Application;
using Oism.Catalog.Application.Products;
using Oism.Catalog.Application.Products.AddSku;
using Oism.Catalog.Application.Products.CreateProduct;
using Oism.Catalog.Application.Products.GetProduct;
using Oism.Catalog.Application.Products.ListProducts;
using Oism.Catalog.Application.Products.UpdateProduct;

namespace Oism.Catalog.Api.Controllers;

public sealed record CreateProductRequest(
    string Name, Guid CategoryId, Guid? BrandId, string? Description, bool HasVariants, IReadOnlyList<SkuInput> Skus);

public sealed record UpdateProductRequest(string Name, Guid CategoryId, Guid? BrandId, string? Description, bool IsActive);

[ApiController]
[Route("products")]
public sealed class ProductsController(
    ListProductsHandler listProducts,
    GetProductHandler getProduct,
    CreateProductHandler createProduct,
    UpdateProductHandler updateProduct,
    AddSkuHandler addSku) : ControllerBase
{
    // Chỉ Owner đặt được giá: docs/architecture/security.md.
    private bool CanSetPrices => User.IsInRole(Roles.Owner);

    [HttpGet]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<PagedResult<ProductListItemDto>>> List(
        CancellationToken ct,
        [FromQuery] string? query = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? brandId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        Ok(await listProducts.Handle(new ListProductsQuery(query, categoryId, brandId, page, pageSize), ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<ProductDto>> Get(Guid id, CancellationToken ct) => Ok(await getProduct.Handle(id, ct));

    [HttpPost]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<ProductDto>> Create(CreateProductRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await createProduct.Handle(
            new CreateProductCommand(
                request.Name, request.CategoryId, request.BrandId, request.Description, request.HasVariants, request.Skus, CanSetPrices),
            ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<ProductDto>> Update(Guid id, UpdateProductRequest request, CancellationToken ct) =>
        Ok(await updateProduct.Handle(
            new UpdateProductCommand(id, request.Name, request.CategoryId, request.BrandId, request.Description, request.IsActive), ct));

    [HttpPost("{id:guid}/skus")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<SkuDto>> AddSku(Guid id, SkuInput request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await addSku.Handle(new AddSkuCommand(id, request, CanSetPrices), ct));
}
