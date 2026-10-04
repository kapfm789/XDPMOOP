using FluentValidation;

namespace Oism.Core.Application.Inventory.ListLedger;

// From và To lọc theo thời điểm ghi, tính cả hai đầu. IncludeCost: chỉ Owner thấy đơn giá.
public sealed record ListLedgerQuery(
    Guid? BranchId, Guid? SkuId, DateTimeOffset? From, DateTimeOffset? To, int Page, int PageSize, bool IncludeCost);

public sealed class ListLedgerValidator : AbstractValidator<ListLedgerQuery>
{
    public ListLedgerValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

// UC-INV-01 AC-2: sổ giao dịch theo thứ tự ghi. Sổ chỉ đọc: không có use case nào sửa hay xóa dòng sổ.
public sealed class ListLedgerHandler(IStockQueries stock)
{
    private static readonly ListLedgerValidator Validator = new();

    public Task<PagedResult<LedgerLineDto>> Handle(ListLedgerQuery query, CancellationToken ct)
    {
        Validator.ValidateAndThrow(query);
        return stock.ListLedgerAsync(query, ct);
    }
}
