using FluentValidation;

namespace Oism.Insights.Application.Forecasting.GetReorderSuggestions;

public sealed record GetReorderSuggestionsQuery(Guid? BranchId, int Page, int PageSize);

public sealed class GetReorderSuggestionsValidator : AbstractValidator<GetReorderSuggestionsQuery>
{
    public GetReorderSuggestionsValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

// Status: Succeeded hoặc Failed. Hai WAPE để trống khi lần chạy lỗi hoặc không có dữ liệu để đánh giá.
public sealed record ForecastRunDto(
    Guid Id, DateTimeOffset RanAt, string Method, string Status, string? Error, decimal? ForecastWape, decimal? BaselineWape);

// Chưa đủ dữ liệu thì ForecastQty và SuggestedReorderQty để trống. Available là tồn khả dụng hiện tại.
public sealed record ReorderSuggestionRow(
    Guid BranchId, Guid SkuId, string? SkuCode, string? Name, bool HasEnoughData, decimal? ForecastQty, int Available,
    int? SuggestedReorderQty);

// Run là lần chạy gần nhất, có thể lỗi; Items lấy từ LastSucceededRun, là lần thành công gần nhất (UC-AI-01 AC-4, AC-5).
public sealed record ReorderSuggestions(
    ForecastRunDto? Run, ForecastRunDto? LastSucceededRun, IReadOnlyList<ReorderSuggestionRow> Items, int Page, int PageSize,
    int Total);

// UC-AI-01: đề xuất nhập hàng của lần chạy dự báo thành công gần nhất, kèm thời điểm và trạng thái của lần chạy gần nhất.
public sealed class GetReorderSuggestionsHandler(IForecastQueries queries)
{
    private static readonly GetReorderSuggestionsValidator Validator = new();

    public async Task<ReorderSuggestions> Handle(GetReorderSuggestionsQuery query, CancellationToken ct)
    {
        Validator.ValidateAndThrow(query);

        var latest = await queries.FindLatestRunAsync(succeededOnly: false, ct);
        var succeeded = latest?.Status == "Succeeded" ? latest : await queries.FindLatestRunAsync(succeededOnly: true, ct);
        if (succeeded is null)
            return new ReorderSuggestions(latest, null, [], query.Page, query.PageSize, 0);

        var (items, total) = await queries.ListResultsAsync(succeeded.Id, query, ct);
        return new ReorderSuggestions(latest, succeeded, items, query.Page, query.PageSize, total);
    }
}
