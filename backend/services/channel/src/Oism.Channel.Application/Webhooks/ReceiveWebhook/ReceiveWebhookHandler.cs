using System.Text;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Oism.Channel.Application.Shops;
using Oism.Channel.Application.Webhooks.Adapters;
using Oism.Channel.Domain;
using Oism.Contracts;
using Oism.SharedKernel;

namespace Oism.Channel.Application.Webhooks.ReceiveWebhook;

// Body là nguyên văn các byte đã nhận: chữ ký được tính trên đúng các byte đó.
public sealed record ReceiveWebhookCommand(string Channel, byte[] Body, string? Signature);

public sealed record WebhookReceipt(string EventId, string Status);

// UC-SIM-01: nhận webhook của sàn, chống trùng, chuẩn hóa về Canonical Order rồi ghi command SubmitOrder vào outbox.
// Trình tự: docs/design/flows/webhook-ingestion.md.
public sealed class ReceiveWebhookHandler(
    IUnitOfWork unitOfWork,
    IChannelShopRepository shops,
    IWebhookEventRepository webhooks,
    IEventPublisher events,
    IClock clock)
{
    public async Task<WebhookReceipt> Handle(ReceiveWebhookCommand command, CancellationToken ct)
    {
        if (!SalesChannels.TryParse(command.Channel, out var channel))
            throw new NotFoundException("kênh bán");
        var adapter = ChannelAdapter.For(channel);

        // Thiếu mã sự kiện hoặc mã shop thì không biết webhook của ai và không chống trùng được: từ chối, không lưu gì.
        using var payload = Parse(command.Body);
        var envelope = adapter.ReadEnvelope(payload.RootElement)
            ?? throw Invalid("Payload thiếu mã sự kiện hoặc mã shop");

        // Từ đây tenant context là tenant của shop. Shop bị tắt coi như không có.
        var shop = await shops.FindForWebhookAsync(channel, envelope.ShopId, ct);
        if (shop is not { IsActive: true })
            throw new NotFoundException("shop");
        if (!shop.Verifies(command.Body, command.Signature))
            throw new InvalidSignatureException();

        try
        {
            return await StoreAsync(shop, adapter, envelope, payload.RootElement, command.Body, ct);
        }
        catch (DuplicateException)
        {
            // Sàn gửi lại cùng webhook: chỉ mục unique (tenant_id, channel, event_id) từ chối, transaction đã rollback
            // nên không có command thứ hai. Trả trạng thái của bản ghi đã có (T03).
            if (await webhooks.FindAsync(channel, envelope.EventId, ct) is not { } existing)
                throw;
            return new WebhookReceipt(existing.EventId, existing.Status.ToString());
        }
    }

    private async Task<WebhookReceipt> StoreAsync(
        ChannelShop shop, ChannelAdapter adapter, WebhookEnvelope envelope, JsonElement payload, byte[] body, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginAsync(ct);

        var now = clock.UtcNow;
        var webhook = WebhookEvent.Receive(shop.Channel, envelope.EventId, Encoding.UTF8.GetString(body), now);
        webhooks.Add(webhook);

        try
        {
            var order = adapter.ToCanonical(payload);
            events.Enqueue(new SubmitOrder(
                shop.Channel.ToString(), order.ExternalOrderId, shop.BranchId, order.PlacedAt,
                order.Lines.Select(line => new SubmitOrderLine(line.SkuCode, line.Quantity, line.UnitPrice, line.Discount)).ToList()));
            webhook.MarkSubmitted(order.ExternalOrderId, now);

            // Đơn này đã được `core` giữ hàng qua một webhook khác mã sự kiện: `core` bỏ qua command lần này mà không phát gì,
            // nên bản ghi nhận luôn kết quả đã biết thay vì nằm lại ở Submitted. Đơn từng bị từ chối thì không: `core` xét lại nó.
            if (await webhooks.FindReservedOrderIdAsync(shop.Channel, order.ExternalOrderId, ct) is { } orderId)
                webhook.MarkReserved(orderId, now);
        }
        catch (InvalidPayloadException invalid)
        {
            // Payload xấu vẫn được lưu và trả 202 để sàn không gửi lại mãi; không phát command.
            webhook.MarkFailed(invalid.Message, now);
        }

        // Bản ghi webhook và thông điệp outbox xuống database cùng lúc: hoặc cả hai, hoặc không gì.
        await transaction.CommitAsync(ct);
        return new WebhookReceipt(webhook.EventId, webhook.Status.ToString());
    }

    private static JsonDocument Parse(byte[] body)
    {
        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            throw Invalid("Body phải là JSON");
        }
    }

    private static ValidationException Invalid(string message) => new([new ValidationFailure("Body", message)]);
}
