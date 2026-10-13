using FluentValidation;
using Oism.Channel.Domain;

namespace Oism.Channel.Application.Webhooks.ListWebhookEvents;

public sealed record ListWebhookEventsQuery(string? Channel, string? Status, int Page, int PageSize);

public sealed class ListWebhookEventsValidator : AbstractValidator<ListWebhookEventsQuery>
{
    public ListWebhookEventsValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Channel)
            .Must(channel => channel is null || SalesChannels.TryParse(channel, out _))
            .WithMessage("Kênh là Shopee, TikTok hoặc Lazada");
        RuleFor(query => query.Status)
            .Must(status => status is null || Enum.GetNames<WebhookStatus>().Contains(status))
            .WithMessage("Trạng thái là Received, Submitted, Reserved, Rejected hoặc Failed");
    }
}

// UC-SIM-01 AC-6: danh sách webhook đã nhận kèm trạng thái và lý do.
public sealed class ListWebhookEventsHandler(IWebhookEventRepository webhooks)
{
    private static readonly ListWebhookEventsValidator Validator = new();

    public Task<PagedResult<WebhookEventDto>> Handle(ListWebhookEventsQuery query, CancellationToken ct)
    {
        Validator.ValidateAndThrow(query);
        return webhooks.ListAsync(
            SalesChannels.TryParse(query.Channel, out var channel) ? channel : null,
            query.Status is null ? null : Enum.Parse<WebhookStatus>(query.Status),
            query.Page, query.PageSize, ct);
    }
}
