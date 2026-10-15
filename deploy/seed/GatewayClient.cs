using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oism.Seed;

// Gọi API qua gateway như frontend: JSON camelCase, access token của người dùng đang đăng nhập.
internal sealed class GatewayClient(HttpClient http)
{
    // Service phía sau gateway có thể chưa khởi động xong, hoặc SKU và chi nhánh vừa tạo chưa tới core qua event.
    private const int Attempts = 90;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string? AccessToken { get; set; }

    public Task<T> GetAsync<T>(string path) => SendAsync<T>(HttpMethod.Get, path);

    public Task<T> PostAsync<T>(string path, object? body = null, string? idempotencyKey = null) =>
        SendAsync<T>(HttpMethod.Post, path, body, idempotencyKey);

    public Task<T> PutAsync<T>(string path, object body) => SendAsync<T>(HttpMethod.Put, path, body);

    public async Task<T> SendAsync<T>(HttpMethod method, string path, object? body = null, string? idempotencyKey = null)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null)
                request.Content = JsonContent.Create(body, body.GetType(), options: Json);
            if (AccessToken is not null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            if (idempotencyKey is not null)
                request.Headers.Add("Idempotency-Key", idempotencyKey);

            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request);
            }
            catch (HttpRequestException) when (attempt < Attempts)
            {
                await Task.Delay(RetryDelay);
                continue;
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    return response.StatusCode == HttpStatusCode.NoContent
                        ? default!
                        : (await response.Content.ReadFromJsonAsync<T>(Json))!;
                }

                var problem = await ReadProblemAsync(response);
                var notReady = (int)response.StatusCode is 502 or 503 or 504 || problem.Code == "reference_not_ready";
                if (notReady && attempt < Attempts)
                {
                    await Task.Delay(RetryDelay);
                    continue;
                }

                throw new SeedApiException((int)response.StatusCode, problem.Code, $"{method} {path}: {problem.Title}");
            }
        }
    }

    private static async Task<Problem> ReadProblemAsync(HttpResponseMessage response)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<Problem>(Json) ?? new Problem(null, null);
        }
        catch (JsonException)
        {
            return new Problem(null, response.ReasonPhrase);
        }
    }

    private sealed record Problem(string? Code, string? Title);
}

internal sealed class SeedApiException(int status, string? code, string message) : Exception($"{status} {code}: {message}")
{
    public int Status { get; } = status;

    public string? Code { get; } = code;
}
