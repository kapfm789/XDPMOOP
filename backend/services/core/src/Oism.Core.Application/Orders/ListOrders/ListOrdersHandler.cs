using FluentValidation;
using Oism.Core.Domain.Orders;

namespace Oism.Core.Application.Orders.ListOrders;

// From và To lọc theo thời điểm tạo đơn, tính cả hai đầu. IncludeCost: chỉ Owner thấy giá vốn của dòng đơn.
public sealed record ListOrdersQuery(
    string? Status, string? Channel, Guid? BranchId, DateTimeOffset? From, DateTimeOffset? To, int Page, int PageSize, bool IncludeCost);

public sealed class ListOrdersValidator : AbstractValidator<ListOrdersQuery>
{
    public ListOrdersValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Status)
            .Must(status => status is null || Enum.GetNames<OrderStatus>().Contains(status))
            .WithMessage("Trạng thái là Draft, Reserved, Confirmed, Completed hoặc Cancelled");
        RuleFor(query => query.Channel)
            .Must(channel => channel is null || Enum.GetNames<OrderChannel>().Contains(channel))
            .WithMessage("Kênh là POS, Admin, Shopee, TikTok hoặc Lazada");
    }
}

// UC-ORD-06 AC-4: danh sách đơn đa kênh, lọc theo trạng thái, kênh, chi nhánh và khoảng thời gian.
public sealed class ListOrdersHandler(IOrderQueries queries)
{
    private static readonly ListOrdersValidator Validator = new();

    public Task<PagedResult<OrderDto>> Handle(ListOrdersQuery query, CancellationToken ct)
    {
        Validator.ValidateAndThrow(query);
        return queries.ListAsync(query, ct);
    }
}
