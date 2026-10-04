using FluentValidation;

namespace Oism.Catalog.Application.Categories.UpsertCategory;

public sealed class UpsertCategoryValidator : AbstractValidator<UpsertCategoryCommand>
{
    public UpsertCategoryValidator() =>
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
}
