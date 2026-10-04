namespace Oism.Catalog.Application.Categories.UpsertCategory;

// Id null là tạo mới.
public sealed record UpsertCategoryCommand(Guid? Id, string Name, Guid? ParentId);
