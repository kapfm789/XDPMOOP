using FluentValidation;

namespace Oism.Insights.Application.Reports.GetInventoryValue;

// IncludeCost: chỉ Owner thấy giá vốn bình quân và giá trị tồn (docs/architecture/security.md).
public sealed record GetInventoryValueQuery(Guid? BranchId, int Page, int PageSize, bool IncludeCost);

public sealed class GetInventoryValueValidator : AbstractValidator<GetInventoryValueQuery>
{
    public GetInventoryValueValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

// AvgCost và Value để trống khi người gọi không được xem giá vốn.
public sealed record InventoryValueRow(
    Guid BranchId, Guid SkuId, string? SkuCode, string? Name, int OnHand, decimal? AvgCost, decimal? Value);

// Khuôn phân trang chung, kèm TotalValue là tổng giá trị của mọi dòng khớp bộ lọc, không chỉ trang này.
public sealed record InventoryValueReport(
    IReadOnlyList<InventoryValueRow> Items, int Page, int PageSize, int Total, decimal? TotalValue);

// UC-REP-02 AC-1: giá trị tồn on_hand × avg_cost theo chi nhánh và SKU, kèm tổng.
public sealed class GetInventoryValueHandler(IReportQueries reports)
{
    private static readonly GetInventoryValueValidator Validator = new();

    public Task<InventoryValueReport> Handle(GetInventoryValueQuery query, CancellationToken ct)
    {
        Validator.ValidateAndThrow(query);
        return reports.ListInventoryValueAsync(query, ct);
    }
}
