using System.Net.Http.Headers;
using Oism.Channel.Application.Simulator.SimulateBurst;
using Oism.Channel.Domain;

namespace Oism.Channel.Infrastructure;

// Simulator đóng vai sàn: gọi chính endpoint webhook của service này qua HTTP, không gọi service khác.
// Địa chỉ gốc lấy từ cấu hình `Simulator:WebhookBaseUrl`.
internal sealed class HttpWebhookSender(HttpClient http) : IWebhookSender
{
    public async Task SendAsync(SalesChannel channel, byte[] body, string signature, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"webhooks/{channel.ToString().ToLowerInvariant()}");
        request.Content = new ByteArrayContent(body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Add("X-Signature", signature);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}
