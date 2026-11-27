using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Oism.Gateway;

// Giới hạn thử đăng nhập theo IP client (W5-16, docs/architecture/security.md mục "Xác thực").
// Policy chỉ gắn vào route identity-login trong ReverseProxy; route khác không bị giới hạn.
public static class LoginRateLimit
{
    public const string Policy = "login";

    public sealed class Settings
    {
        public int PermitLimit { get; set; } = 20;
        public int WindowSeconds { get; set; } = 60;
    }

    public static IServiceCollection AddLoginRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        // Đọc qua options lúc có request, không đọc ngay lúc đăng ký: cấu hình nạp sau (biến môi trường, test) vẫn có hiệu lực.
        services.Configure<Settings>(configuration.GetSection("RateLimit:Login"));

        return services.AddRateLimiter(options =>
        {
            options.AddPolicy(Policy, context =>
            {
                var settings = context.RequestServices.GetRequiredService<IOptions<Settings>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.PermitLimit,
                        Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                        QueueLimit = 0,
                    });
            });

            options.OnRejected = async (context, ct) =>
            {
                var http = context.HttpContext;
                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    http.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

                // Cùng dạng ProblemDetails kèm `code` như mọi lỗi khác (docs/design/api/README.md).
                var problems = http.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problems.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = http,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Đăng nhập quá nhiều lần, thử lại sau",
                        Extensions = { ["code"] = "too_many_requests" },
                    },
                });
            };
        });
    }
}
