using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Messaging;
using Oism.BuildingBlocks.Tenancy;
using Oism.Channel.Application.Shops;
using Oism.Channel.Domain;
using Oism.Channel.Infrastructure;

namespace Oism.Channel.IntegrationTests;

internal static class TestKit
{
    // Mỗi lần gọi là một scope, tức một DbContext mới mang tenant của test, như một request.
    public static async Task<T> QueryAsync<T>(this ApiFactory factory, Guid tenantId, Func<ChannelDbContext, Task<T>> query)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
        return await query(scope.ServiceProvider.GetRequiredService<ChannelDbContext>());
    }

    // Shop qua POST /channel-shops: phản hồi mang secret, chỉ lần này. Mã shop là unique trên toàn hệ thống nên mặc định sinh mới.
    public static async Task<ChannelShopDto> CreateShopAsync(
        this HttpClient owner, string channel = "Shopee", string? shopId = null, Guid? branchId = null)
    {
        var response = await owner.PostAsJsonAsync(
            "/channel-shops", new { channel, shopId = shopId ?? $"shop-{Guid.NewGuid():N}", branchId = branchId ?? Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChannelShopDto>())!;
    }

    // Chữ ký tính độc lập với code của service: HMAC-SHA256 của nguyên văn body với secret của shop, hex chữ thường
    // (docs/design/api/channel.md mục "Chữ ký").
    public static string Sign(string body, string secret) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

    // Webhook như sàn gửi: không JWT, chỉ có X-Signature. signature null: ký đúng bằng secret; chuỗi rỗng: không gửi header.
    public static Task<HttpResponseMessage> SendWebhookAsync(
        this ApiFactory factory, string channel, string body, string secret, string? signature = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/webhooks/{channel}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (signature != string.Empty)
            request.Headers.Add("X-Signature", signature ?? Sign(body, secret));
        return factory.CreateClient().SendAsync(request);
    }

    // Kiểm 202 và trả `status` của phản hồi.
    public static async Task<string> AcceptedStatusAsync(this Task<HttpResponseMessage> sending, string eventId)
    {
        var response = await sending;
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var receipt = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(eventId, receipt.GetProperty("eventId").GetString());
        return receipt.GetProperty("status").GetString()!;
    }

    // Payload theo hình dạng của từng sàn ở docs/design/api/channel.md, một dòng hàng.
    public static string Shopee(string eventId, string shopId, string orderId, string sku = "AO-A-DEN-M", int quantity = 1) =>
        $$"""{"event_id":"{{eventId}}","shop_id":"{{shopId}}","ordersn":"{{orderId}}","create_time":1792466127,"item_list":[{"model_sku":"{{sku}}","quantity":{{quantity}},"price":150000,"discount":0}]}""";

    public static string TikTok(string eventId, string shopId, string orderId) =>
        $$$"""{"id":"{{{eventId}}}","shop_id":"{{{shopId}}}","data":{"order_id":"{{{orderId}}}","create_time":1792466127,"line_items":[{"seller_sku":"AO-A-DEN-M","quantity":1,"sale_price":150000,"seller_discount":0}]}}""";

    public static string Lazada(string eventId, string shopId, string orderId) =>
        $$$"""{"message_id":"{{{eventId}}}","seller_id":"{{{shopId}}}","order":{"order_number":"{{{orderId}}}","created_at":"2026-10-20T03:15:27Z","items":[{"sku":"AO-A-DEN-M","qty":1,"paid_price":150000,"voucher":0}]}}""";

    public static Task<List<WebhookEvent>> WebhooksAsync(this ApiFactory factory, Guid tenantId) =>
        factory.QueryAsync(tenantId, db => db.WebhookEvents.AsNoTracking().OrderBy(webhook => webhook.ReceivedAt).ToListAsync());

    // Thông điệp loại TPayload trong outbox của tenant.
    public static async Task<List<TPayload>> OutboxAsync<TPayload>(this ApiFactory factory, Guid tenantId)
    {
        var messages = await factory.QueryAsync(tenantId, db => db.Set<OutboxMessage>().AsNoTracking()
            .Where(message => message.Type == typeof(TPayload).Name).OrderBy(message => message.OccurredAt).ToListAsync());
        return messages.Select(message => JsonSerializer.Deserialize<TPayload>(message.Payload, EventEnvelope.JsonOptions)!).ToList();
    }

    // Kiểm mã HTTP và trường `code` của ProblemDetails (docs/design/api/README.md).
    public static async Task AssertProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }
}
