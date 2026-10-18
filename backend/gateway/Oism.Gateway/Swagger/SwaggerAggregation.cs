using System.Text.Json.Nodes;

namespace Oism.Gateway.Swagger;

// Gộp Swagger của 5 service ở /swagger (docs/plans/backend-gateway.md). Tài liệu của từng service lấy từ địa chỉ cluster
// trong ReverseProxy; `servers` được đặt thành tiền tố /api/<service> để "Try it out" đi qua gateway như frontend gọi.
public static class SwaggerAggregation
{
    private static readonly string[] Services = ["identity", "catalog", "core", "channel", "insights"];

    public static IServiceCollection AddSwaggerAggregation(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(SwaggerAggregation), client => client.Timeout = TimeSpan.FromSeconds(10));
        return services;
    }

    public static WebApplication UseSwaggerAggregation(this WebApplication app)
    {
        app.UseSwaggerUI(options =>
        {
            options.DocumentTitle = "OISM API";
            foreach (var service in Services)
                options.SwaggerEndpoint($"/swagger/{service}/swagger.json", service);
        });

        // Tài liệu không mang dữ liệu nghiệp vụ và chỉ bật ở Development: công khai như health check.
        app.MapGet("/swagger/{service}/swagger.json", async (
                string service, IConfiguration configuration, IHttpClientFactory httpClients, CancellationToken ct) =>
            {
                var address = Services.Contains(service)
                    ? configuration[$"ReverseProxy:Clusters:{service}:Destinations:default:Address"]
                    : null;
                if (address is null)
                    return Results.NotFound();

                JsonObject? document;
                try
                {
                    document = await httpClients.CreateClient(nameof(SwaggerAggregation))
                        .GetFromJsonAsync<JsonObject>($"{address.TrimEnd('/')}/swagger/v1/swagger.json", ct);
                }
                catch (HttpRequestException)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status502BadGateway, title: $"Không lấy được Swagger của {service}");
                }

                if (document is null)
                    return Results.Problem(
                        statusCode: StatusCodes.Status502BadGateway, title: $"Không lấy được Swagger của {service}");

                document["servers"] = new JsonArray(new JsonObject { ["url"] = $"/api/{service}" });
                return Results.Json(document);
            })
            .AllowAnonymous();

        return app;
    }
}
