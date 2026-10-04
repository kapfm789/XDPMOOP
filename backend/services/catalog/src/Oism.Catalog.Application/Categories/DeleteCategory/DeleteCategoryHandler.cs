using Oism.Catalog.Domain;
using Oism.SharedKernel;

namespace Oism.Catalog.Application.Categories.DeleteCategory;

public sealed class DeleteCategoryHandler(IUnitOfWork unitOfWork, ICategoryRepository categories)
{
    public async Task Handle(DeleteCategoryCommand command, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginAsync(ct);

        var all = await categories.ListForUpdateAsync(ct);
        var category = all.SingleOrDefault(other => other.Id == command.Id) ?? throw new NotFoundException("danh mục");

        // Vế "còn sản phẩm" của UC-PROD-01 AC-3 được kiểm từ W1-07, khi có bảng products.
        if (all.Any(other => other.ParentId == category.Id))
            throw new CategoryInUseException();

        categories.Remove(category);
        await transaction.CommitAsync(ct);
    }
}
