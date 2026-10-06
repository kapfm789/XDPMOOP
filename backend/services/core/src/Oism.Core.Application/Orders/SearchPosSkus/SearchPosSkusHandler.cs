using FluentValidation;
using Oism.Core.Application.Inventory;

namespace Oism.Core.Application.Orders.SearchPosSkus;

// Query bỏ trống thì trả các SKU đầu tiên theo mã.
public sealed record SearchPosSkusQuery(Guid BranchId, string? Query);

// Một SKU đang bán kèm tồn khả dụng tại chi nhánh: docs/design/api/core.md mục "POS".
public sealed record PosSkuDto(Guid SkuId, string SkuCode, string Name, IReadOnlyList<string> Barcodes, decimal RetailPrice, int Available);

public sealed class SearchPosSkusValidator : AbstractValidator<SearchPosSkusQuery>
{
    public SearchPosSkusValidator()
    {
        RuleFor(query => query.BranchId).NotEmpty().WithMessage("Chọn chi nhánh");
        RuleFor(query => query.Query).MaximumLength(100);
    }
}

// UC-POS-01: tìm theo một phần tên, mã SKU hoặc mã vạch. Tồn khả dụng ở đây chỉ để báo sớm;
// POST /pos/checkout mới là nơi quyết định.
public sealed class SearchPosSkusHandler(IStockQueries queries)
{
    public const int Limit = 20;

    private static readonly SearchPosSkusValidator Validator = new();

    public Task<IReadOnlyList<PosSkuDto>> Handle(SearchPosSkusQuery query, CancellationToken ct)
    {
        Validator.ValidateAndThrow(query);
        return queries.SearchPosSkusAsync(query, Limit, ct);
    }
}
