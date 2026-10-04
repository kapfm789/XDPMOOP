using Microsoft.EntityFrameworkCore;
using Oism.BuildingBlocks.Tenancy;
using Oism.Catalog.Application.Categories;
using Oism.Catalog.Domain;

namespace Oism.Catalog.Infrastructure.Repositories;

internal sealed class CategoryRepository(CatalogDbContext db, ITenantContext tenant) : ICategoryRepository
{
    public async Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken ct) =>
        await db.Categories.AsNoTracking()
            .OrderBy(category => category.SortOrder).ThenBy(category => category.Name)
            .Select(category => new CategoryDto(category.Id, category.Name, category.ParentId, category.SortOrder))
            .ToListAsync(ct);

    // Điều kiện tenant_id nằm ngay trong câu khóa để chỉ khóa dòng của tenant hiện tại;
    // Global Query Filter vẫn được áp ở lớp ngoài.
    public Task<List<Category>> ListForUpdateAsync(CancellationToken ct) =>
        db.Categories
            .FromSql($"SELECT * FROM catalog.categories WHERE tenant_id = {tenant.TenantId} ORDER BY id FOR UPDATE")
            .ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct) => db.Categories.AnyAsync(category => category.Id == id, ct);

    public void Add(Category category) => db.Add(category);

    public void Remove(Category category) => db.Remove(category);
}
