using FluentValidation;

namespace Oism.Insights.Application.Reports.GetGrossProfit;

public enum GrossProfitGroupBy
{
    None,
    Day,
    Branch,
    Channel,
    Sku,
}

// From và To là ngày theo giờ Việt Nam, tính cả hai đầu. GroupBy: day, branch, channel, sku hoặc bỏ trống.
public sealed record GetGrossProfitQuery(
    DateOnly? From, DateOnly? To, Guid? BranchId, string? Channel, Guid? SkuId, string? GroupBy);

public sealed class GetGrossProfitValidator : AbstractValidator<GetGrossProfitQuery>
{
    private static readonly string[] GroupByValues = ["day", "branch", "channel", "sku"];

    public GetGrossProfitValidator()
    {
        RuleFor(query => query.From).NotNull();
        RuleFor(query => query.To).NotNull().GreaterThanOrEqualTo(query => query.From).When(query => query.From is not null);
        RuleFor(query => query.GroupBy)
            .Must(groupBy => groupBy is null || GroupByValues.Contains(groupBy))
            .WithMessage("groupBy phải là day, branch, channel hoặc sku");
    }
}

// Bộ lọc đã kiểm, chuyển cho phần truy vấn ở Infrastructure.
public sealed record GrossProfitFilter(
    DateOnly From, DateOnly To, Guid? BranchId, string? Channel, Guid? SkuId, GrossProfitGroupBy GroupBy);

// Key của dòng: ngày yyyy-MM-dd, branchId, tên kênh hoặc skuId tùy groupBy. SkuCode và Name chỉ có khi nhóm theo SKU.
public sealed record GrossProfitRow(
    string Key, string? SkuCode, string? Name, int Quantity, decimal Revenue, decimal Cogs, decimal GrossProfit);

public sealed record GrossProfitTotals(int Quantity, decimal Revenue, decimal Cogs, decimal GrossProfit);

public sealed record GrossProfitReport(GrossProfitTotals Totals, IReadOnlyList<GrossProfitRow> Rows);

// UC-REP-01: doanh thu thuần, giá vốn hàng bán và lợi nhuận gộp theo công thức của ADR-0008.
public sealed class GetGrossProfitHandler(IReportQueries reports)
{
    private static readonly GetGrossProfitValidator Validator = new();

    public async Task<GrossProfitReport> Handle(GetGrossProfitQuery query, CancellationToken ct)
    {
        Validator.ValidateAndThrow(query);
        var groupBy = query.GroupBy is null ? GrossProfitGroupBy.None : Enum.Parse<GrossProfitGroupBy>(query.GroupBy, ignoreCase: true);

        var rows = await reports.SumGrossProfitAsync(
            new GrossProfitFilter(query.From!.Value, query.To!.Value, query.BranchId, query.Channel, query.SkuId, groupBy), ct);

        var totals = new GrossProfitTotals(
            rows.Sum(row => row.Quantity), rows.Sum(row => row.Revenue), rows.Sum(row => row.Cogs), rows.Sum(row => row.GrossProfit));
        return new GrossProfitReport(totals, groupBy == GrossProfitGroupBy.None ? [] : rows);
    }
}
