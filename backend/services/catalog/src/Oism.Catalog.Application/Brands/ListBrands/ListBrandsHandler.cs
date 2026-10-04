namespace Oism.Catalog.Application.Brands.ListBrands;

public sealed class ListBrandsHandler(IBrandRepository brands)
{
    public Task<IReadOnlyList<BrandDto>> Handle(CancellationToken ct) => brands.ListAsync(ct);
}
