using FluentValidation;
using Oism.Core.Domain.Inventory;

namespace Oism.Core.Application.Inventory.ListPurchaseReceipts;

public sealed record ListPurchaseReceiptsQuery(Guid? BranchId, string? Status, int Page, int PageSize);

public sealed class ListPurchaseReceiptsValidator : AbstractValidator<ListPurchaseReceiptsQuery>
{
    public ListPurchaseReceiptsValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Status)
            .Must(status => status is null || Enum.GetNames<PurchaseReceiptStatus>().Contains(status))
            .WithMessage("Trạng thái là Draft hoặc Confirmed");
    }
}

public sealed class ListPurchaseReceiptsHandler(IStockQueries queries)
{
    private static readonly ListPurchaseReceiptsValidator Validator = new();

    public Task<PagedResult<PurchaseReceiptDto>> Handle(ListPurchaseReceiptsQuery query, CancellationToken ct)
    {
        Validator.ValidateAndThrow(query);
        return queries.ListPurchaseReceiptsAsync(query, ct);
    }
}
