using Oism.Catalog.Domain;

namespace Oism.Catalog.Application.Products;

public interface IProductRepository
{
    // Sản phẩm của tenant xếp theo tên. query khớp tên sản phẩm hoặc mã SKU, không phân biệt hoa thường.
    Task<PagedResult<ProductListItemDto>> ListAsync(
        string? query, Guid? categoryId, Guid? brandId, int page, int pageSize, CancellationToken ct);

    // Sản phẩm kèm mọi SKU và mã vạch, chỉ để đọc.
    Task<Product?> GetAsync(Guid id, CancellationToken ct);

    // Khóa dòng sản phẩm rồi nạp mọi SKU và mã vạch của nó. Mọi thay đổi SKU, mã vạch, giá đều đi qua khóa này,
    // nên hai lần sửa đồng thời phải lần lượt và version của SKU tăng đúng từng bước.
    Task<Product?> GetForUpdateAsync(Guid id, CancellationToken ct);

    // Như GetForUpdateAsync, tìm theo một SKU của sản phẩm.
    Task<Product?> GetBySkuForUpdateAsync(Guid skuId, CancellationToken ct);

    Task<bool> AnyInCategoryAsync(Guid categoryId, CancellationToken ct);

    // Số thứ tự kế tiếp cho mã EAN-13 tự sinh của tenant. Giữ khóa theo tenant tới hết transaction,
    // để hai request cùng lúc không nhận cùng một số.
    Task<long> NextEan13SequenceAsync(CancellationToken ct);

    void Add(Product product);
}
