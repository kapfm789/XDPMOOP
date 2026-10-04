using Oism.Catalog.Domain;

namespace Oism.Catalog.Application.Categories;

public sealed record CategoryDto(Guid Id, string Name, Guid? ParentId, int SortOrder)
{
    public static CategoryDto From(Category category) =>
        new(category.Id, category.Name, category.ParentId, category.SortOrder);
}

// Một nút của cây danh mục trả về từ GET /categories.
public sealed record CategoryNodeDto(Guid Id, string Name, Guid? ParentId, int SortOrder, IReadOnlyList<CategoryNodeDto> Children);
