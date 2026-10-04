using Oism.Catalog.Application.Products;
using Oism.Catalog.Domain;
using Oism.SharedKernel;

namespace Oism.Catalog.Application.Categories.DeleteCategory;

public sealed class DeleteCategoryHandler(IUnitOfWork unitOfWork, ICategoryRepository categories, IProductRepository products)
{
    public async Task Handle(DeleteCategoryCommand command, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginAsync(ct);

        var all = await categories.ListForUpdateAsync(ct);
        var category = all.SingleOrDefault(other => other.Id == command.Id) ?? throw new NotFoundException("danh mục");

        // UC-PROD-01 AC-3: còn danh mục con hoặc còn sản phẩm thì không xóa.
        // ponytail: sản phẩm được tạo vào danh mục đúng lúc đang xóa thì khóa ngoại chặn và trả 500 thay vì 409;
        // cần 409 thì đổi lỗi vi phạm khóa ngoại ở CommitAsync.
        if (all.Any(other => other.ParentId == category.Id) || await products.AnyInCategoryAsync(category.Id, ct))
            throw new CategoryInUseException();

        categories.Remove(category);
        await transaction.CommitAsync(ct);
    }
}
