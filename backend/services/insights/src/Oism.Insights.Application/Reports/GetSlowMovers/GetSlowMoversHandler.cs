using FluentValidation;
using Oism.Insights.Application.Reports.GetTopSellers;
using Oism.Insights.Domain.Sales;

namespace Oism.Insights.Application.Reports.GetSlowMovers;

// Cùng tham số với bán chạy: Days tính cả hôm nay theo giờ Việt Nam, Limit là số SKU tối đa.
public sealed record GetSlowMoversQuery(int Days, Guid? BranchId, int Limit);

public sealed class GetSlowMoversValidator : AbstractValidator<GetSlowMoversQuery>
{
    public GetSlowMoversValidator()
    {
        RuleFor(query => query.Days).InclusiveBetween(1, 365);
        RuleFor(query => query.Limit).InclusiveBetween(1, 100);
    }
}

// OnHand là tồn thực tế hiện tại, cộng mọi chi nhánh khi không lọc chi nhánh.
public sealed record SlowMoverRow(Guid SkuId, string? SkuCode, string? Name, int Quantity, int OnHand);

// UC-REP-02 AC-3: chỉ SKU còn tồn, xếp tăng dần theo số lượng bán trong N ngày gần nhất; SKU không bán được gì đứng đầu.
public sealed class GetSlowMoversHandler(IReportQueries reports, IClock clock)
{
    private static readonly GetSlowMoversValidator Validator = new();

    public Task<IReadOnlyList<SlowMoverRow>> Handle(GetSlowMoversQuery query, CancellationToken ct)
    {
        Validator.ValidateAndThrow(query);
        var since = ReportCalendar.StartOfLastDays(clock.UtcNow, query.Days);
        return reports.ListSlowMoversAsync(new SalesWindowFilter(since, query.BranchId, query.Limit), ct);
    }
}
