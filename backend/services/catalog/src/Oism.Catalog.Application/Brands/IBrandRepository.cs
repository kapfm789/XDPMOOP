using Oism.Catalog.Domain;

namespace Oism.Catalog.Application.Brands;

public interface IBrandRepository
{
    // Mọi thương hiệu của tenant, xếp theo tên.
    Task<IReadOnlyList<BrandDto>> ListAsync(CancellationToken ct);

    Task<Brand?> GetAsync(Guid id, CancellationToken ct);

    void Add(Brand brand);
}
