using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oism.BuildingBlocks.Auth;
using Oism.Catalog.Application.Categories;
using Oism.Catalog.Application.Categories.DeleteCategory;
using Oism.Catalog.Application.Categories.GetCategoryTree;
using Oism.Catalog.Application.Categories.UpsertCategory;

namespace Oism.Catalog.Api.Controllers;

public sealed record CategoryRequest(string Name, Guid? ParentId);

[ApiController]
[Route("categories")]
public sealed class CategoriesController(
    GetCategoryTreeHandler getCategoryTree, UpsertCategoryHandler upsertCategory, DeleteCategoryHandler deleteCategory)
    : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<IReadOnlyList<CategoryNodeDto>>> GetTree(CancellationToken ct) =>
        Ok(await getCategoryTree.Handle(ct));

    [HttpPost]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<CategoryDto>> Create(CategoryRequest request, CancellationToken ct) =>
        StatusCode(
            StatusCodes.Status201Created,
            await upsertCategory.Handle(new UpsertCategoryCommand(null, request.Name, request.ParentId), ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<CategoryDto>> Update(Guid id, CategoryRequest request, CancellationToken ct) =>
        Ok(await upsertCategory.Handle(new UpsertCategoryCommand(id, request.Name, request.ParentId), ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await deleteCategory.Handle(new DeleteCategoryCommand(id), ct);
        return NoContent();
    }
}
