using FluentValidation;

namespace Oism.Core.Application.Inventory.ListStock;

// IncludeCost: chỉ Owner thấy giá vốn bình quân.
public sealed record ListStockQuery(Guid? BranchId, Guid? SkuId, string? Query, int Page, int PageSize, bool IncludeCost);

public sealed class ListStockValidator : AbstractValidator<ListStockQuery>
{
    public ListStockValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

// UC-INV-01 AC-1: tồn hiện tại theo chi nhánh và SKU.
public sealed class ListStockHandler(IStockQueries stock)
{
    private static readonly ListStockValidator Validator = new();

    public Task<PagedResult<StockDto>> Handle(ListStockQuery query, CancellationToken ct)
    {
        Validator.ValidateAndThrow(query);
        return stock.ListStockAsync(query, ct);
    }
}
