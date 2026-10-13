using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oism.Channel.Application.Webhooks.ReceiveWebhook;

namespace Oism.Channel.Api.Controllers;

// UC-SIM-01: webhook của sàn. Không mang JWT; tenant xác định qua ChannelShop và chữ ký HMAC trong header X-Signature
// (docs/architecture/security.md mục "Webhook").
[ApiController]
[Route("webhooks")]
public sealed class WebhooksController(ReceiveWebhookHandler receiveWebhook) : ControllerBase
{
    // Trả 202 ngay; kết quả giữ hàng về sau bằng event. Gửi lại cùng mã sự kiện trả 202 với trạng thái của bản ghi đã có.
    [HttpPost("{channel}")]
    [AllowAnonymous]
    public async Task<ActionResult<WebhookReceipt>> Receive(string channel, CancellationToken ct)
    {
        // Chữ ký tính trên nguyên văn body, nên đọc byte thô thay vì để model binding đọc JSON.
        using var body = new MemoryStream();
        await Request.Body.CopyToAsync(body, ct);

        return Accepted(await receiveWebhook.Handle(
            new ReceiveWebhookCommand(channel, body.ToArray(), Request.Headers["X-Signature"]), ct));
    }
}
