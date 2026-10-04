using FluentValidation;
using FluentValidation.Results;
using Oism.Catalog.Domain;
using Oism.SharedKernel;

namespace Oism.Catalog.Application.Categories.UpsertCategory;

public sealed class UpsertCategoryHandler(IUnitOfWork unitOfWork, ICategoryRepository categories)
{
    private static readonly UpsertCategoryValidator Validator = new();

    public async Task<CategoryDto> Handle(UpsertCategoryCommand command, CancellationToken ct)
    {
        Validator.ValidateAndThrow(command);
        var name = command.Name.Trim();

        await using var transaction = await unitOfWork.BeginAsync(ct);

        var all = await categories.ListForUpdateAsync(ct);
        if (command.ParentId is { } parentId && all.All(other => other.Id != parentId))
            throw new NotFoundException("danh mục cha");

        Category category;
        if (command.Id is { } id)
        {
            category = all.SingleOrDefault(other => other.Id == id) ?? throw new NotFoundException("danh mục");
            if (!category.CanMoveUnder(command.ParentId, all.ToDictionary(other => other.Id, other => other.ParentId)))
            {
                throw new ValidationException([new ValidationFailure(
                    nameof(command.ParentId), "Danh mục cha không được là chính nó hay danh mục con của nó")]);
            }

            category.Update(name, command.ParentId);
        }
        else
        {
            category = new Category(name, command.ParentId);
            categories.Add(category);
        }

        // Trùng tên trong cùng danh mục cha: chỉ mục unique từ chối, CommitAsync ném DuplicateException.
        await transaction.CommitAsync(ct);
        return CategoryDto.From(category);
    }
}
