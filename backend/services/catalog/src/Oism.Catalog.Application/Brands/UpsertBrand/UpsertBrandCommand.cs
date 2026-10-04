namespace Oism.Catalog.Application.Brands.UpsertBrand;

// Id null là tạo mới.
public sealed record UpsertBrandCommand(Guid? Id, string Name);
