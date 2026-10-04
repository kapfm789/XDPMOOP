using Microsoft.EntityFrameworkCore;
using Oism.Catalog.Application.Brands;
using Oism.Catalog.Domain;

namespace Oism.Catalog.Infrastructure.Repositories;

internal sealed class BrandRepository(CatalogDbContext db) : IBrandRepository
{
    public async Task<IReadOnlyList<BrandDto>> ListAsync(CancellationToken ct) =>
        await db.Brands.AsNoTracking()
            .OrderBy(brand => brand.Name)
            .Select(brand => new BrandDto(brand.Id, brand.Name))
            .ToListAsync(ct);

    public Task<Brand?> GetAsync(Guid id, CancellationToken ct) =>
        db.Brands.SingleOrDefaultAsync(brand => brand.Id == id, ct);

    public void Add(Brand brand) => db.Add(brand);
}
