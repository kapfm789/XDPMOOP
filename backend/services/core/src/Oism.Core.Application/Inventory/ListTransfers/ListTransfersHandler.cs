using FluentValidation;
using Oism.Core.Domain.Inventory;

namespace Oism.Core.Application.Inventory.ListTransfers;

// IncludeCost: chỉ Owner thấy giá vốn mang theo trên dòng phiếu.
public sealed record ListTransfersQuery(string? Status, int Page, int PageSize, bool IncludeCost);

public sealed class ListTransfersValidator : AbstractValidator<ListTransfersQuery>
{
    public ListTransfersValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Status)
            .Must(status => status is null || Enum.GetNames<StockTransferStatus>().Contains(status))
            .WithMessage("Trạng thái là Draft, InTransit hoặc Received");
    }
}

public sealed class ListTransfersHandler(IStockQueries queries)
{
    private static readonly ListTransfersValidator Validator = new();

    public Task<PagedResult<TransferDto>> Handle(ListTransfersQuery query, CancellationToken ct)
    {
        Validator.ValidateAndThrow(query);
        return queries.ListTransfersAsync(query, ct);
    }
}
