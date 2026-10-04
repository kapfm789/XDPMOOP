namespace Oism.Catalog.Application.Categories.GetCategoryTree;

public sealed class GetCategoryTreeHandler(ICategoryRepository categories)
{
    public async Task<IReadOnlyList<CategoryNodeDto>> Handle(CancellationToken ct)
    {
        var childrenOf = (await categories.ListAsync(ct)).ToLookup(category => category.ParentId);
        return Nodes(null);

        IReadOnlyList<CategoryNodeDto> Nodes(Guid? parentId) => childrenOf[parentId]
            .Select(category => new CategoryNodeDto(
                category.Id, category.Name, category.ParentId, category.SortOrder, Nodes(category.Id)))
            .ToList();
    }
}
