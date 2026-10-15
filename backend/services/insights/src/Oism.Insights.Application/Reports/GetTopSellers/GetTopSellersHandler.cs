using FluentValidation;
using Oism.Insights.Domain.Sales;

namespace Oism.Insights.Application.Reports.GetTopSellers;

// Days: số ngày gần nhất, tính cả hôm nay theo giờ Việt Nam. Limit: số SKU tối đa.
public sealed record GetTopSellersQuery(int Days, Guid? BranchId, int Limit);

public sealed class GetTopSellersValidator : AbstractValidator<GetTopSellersQuery>
{
    public GetTopSellersValidator()
    {
        RuleFor(query => query.Days).InclusiveBetween(1, 365);
        RuleFor(query => query.Limit).InclusiveBetween(1, 100);
    }
}

// Bộ lọc đã kiểm, chuyển cho phần truy vấn ở Infrastructure. Since là 00:00 giờ Việt Nam của ngày đầu cửa sổ.
public sealed record SalesWindowFilter(DateTimeOffset Since, Guid? BranchId, int Limit);

public sealed record TopSellerRow(Guid SkuId, string? SkuCode, string? Name, int Quantity);

// UC-REP-02 AC-2: SKU xếp giảm dần theo số lượng bán trong N ngày gần nhất (ADR-0008).
public sealed class GetTopSellersHandler(IReportQueries reports, IClock clock)
{
    private static readonly GetTopSellersValidator Validator = new();

    public Task<IReadOnlyList<TopSellerRow>> Handle(GetTopSellersQuery query, CancellationToken ct)
    {
        Validator.ValidateAndThrow(query);
        var since = ReportCalendar.StartOfLastDays(clock.UtcNow, query.Days);
        return reports.ListTopSellersAsync(new SalesWindowFilter(since, query.BranchId, query.Limit), ct);
    }
}
