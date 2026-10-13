using FluentValidation;
using Oism.Core.Domain.Inventory;

namespace Oism.Core.Application.Inventory.ListStocktakes;

public sealed record ListStocktakesQuery(Guid? BranchId, string? Status, int Page, int PageSize);

public sealed class ListStocktakesValidator : AbstractValidator<ListStocktakesQuery>
{
    public ListStocktakesValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Status)
            .Must(status => status is null || Enum.GetNames<StocktakeStatus>().Contains(status))
            .WithMessage("Trạng thái là Open hoặc Posted");
    }
}

public sealed class ListStocktakesHandler(IStockQueries queries)
{
    private static readonly ListStocktakesValidator Validator = new();

    public Task<PagedResult<StocktakeDto>> Handle(ListStocktakesQuery query, CancellationToken ct)
    {
        Validator.ValidateAndThrow(query);
        return queries.ListStocktakesAsync(query, ct);
    }
}
