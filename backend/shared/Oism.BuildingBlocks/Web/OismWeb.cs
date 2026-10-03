using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Auth;
using Oism.BuildingBlocks.Tenancy;

namespace Oism.BuildingBlocks.Web;

// Phần Program.cs giống nhau của mọi service và gateway.
public static class OismWeb
{
    public static IServiceCollection AddOismWeb(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddOismAuth(configuration);
        services.AddHealthChecks();
        services.AddExceptionHandler<OismExceptionHandler>();
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            // Lỗi không qua exception (401, 403, 404 của pipeline) vẫn phải mang `code`: docs/design/api/README.md.
            var problem = context.ProblemDetails;
            if (!problem.Extensions.TryGetValue("code", out var code))
            {
                code = problem.Status switch
                {
                    StatusCodes.Status401Unauthorized => "unauthenticated",
                    StatusCodes.Status403Forbidden => "forbidden",
                    StatusCodes.Status404NotFound => "not_found",
                    _ => null,
                };
            }

            if (code is null)
                return;

            problem.Extensions["code"] = code;
            problem.Type = $"https://oism.local/errors/{code}";
        });

        return services;
    }

    public static WebApplication UseOismWeb(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseTenantContext();
        app.UseAuthorization();
        app.MapHealthChecks("/health").AllowAnonymous();
        return app;
    }
}
