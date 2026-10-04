using Oism.Catalog.Domain;

namespace Oism.Catalog.Application.Categories;

public interface ICategoryRepository
{
    // Mọi danh mục của tenant, xếp theo sort_order rồi tên.
    Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken ct);

    // Khóa mọi danh mục của tenant. Sửa cây là thao tác trên cả cây (kiểm vòng lặp, kiểm còn danh mục con),
    // nên hai lần sửa đồng thời phải lần lượt.
    Task<List<Category>> ListForUpdateAsync(CancellationToken ct);

    void Add(Category category);

    void Remove(Category category);
}
