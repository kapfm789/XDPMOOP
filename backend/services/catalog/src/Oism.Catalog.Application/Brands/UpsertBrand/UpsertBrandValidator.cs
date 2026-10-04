using FluentValidation;

namespace Oism.Catalog.Application.Brands.UpsertBrand;

public sealed class UpsertBrandValidator : AbstractValidator<UpsertBrandCommand>
{
    public UpsertBrandValidator() =>
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
}
