using FluentValidation;

namespace Oism.Catalog.Application.Products.ListProducts;

public sealed record ListProductsQuery(string? Query, Guid? CategoryId, Guid? BrandId, int Page = 1, int PageSize = 20);

public sealed class ListProductsValidator : AbstractValidator<ListProductsQuery>
{
    public ListProductsValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class ListProductsHandler(IProductRepository products)
{
    private static readonly ListProductsValidator Validator = new();

    public Task<PagedResult<ProductListItemDto>> Handle(ListProductsQuery query, CancellationToken ct)
    {
        Validator.ValidateAndThrow(query);
        return products.ListAsync(query.Query, query.CategoryId, query.BrandId, query.Page, query.PageSize, ct);
    }
}
