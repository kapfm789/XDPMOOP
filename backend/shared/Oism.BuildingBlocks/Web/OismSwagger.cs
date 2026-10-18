using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Oism.BuildingBlocks.Web;

// Swagger của từng service là tài liệu sinh từ code (docs/design/api/README.md); gateway gộp năm tài liệu ở /swagger.
// Mỗi endpoint có policy mang yêu cầu Bearer và dòng vai trò được gọi, để đối chiếu với cột "Vai trò" của docs/design/api/.
public static class OismSwagger
{
    public const string BearerScheme = "Bearer";

    public static IServiceCollection AddOismSwagger(this IServiceCollection services, string service) =>
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = $"OISM {service}", Version = "v1" });
            options.AddSecurityDefinition(BearerScheme, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Access token lấy từ POST /api/identity/auth/login",
            });
            options.OperationFilter<RoleOperationFilter>();
        });
}

internal sealed class RoleOperationFilter(IOptions<AuthorizationOptions> authorization) : IOperationFilter
{
    private static readonly OpenApiSecurityRequirement Bearer = new()
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = OismSwagger.BearerScheme },
        }] = [],
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any())
        {
            operation.Description = Append(operation.Description, "Công khai, không cần token.");
            return;
        }

        var roles = metadata.OfType<IAuthorizeData>()
            .Select(data => data.Policy)
            .OfType<string>()
            .Select(authorization.Value.GetPolicy)
            .OfType<AuthorizationPolicy>()
            .SelectMany(policy => policy.Requirements.OfType<RolesAuthorizationRequirement>())
            .SelectMany(requirement => requirement.AllowedRoles)
            .Distinct()
            .ToArray();

        operation.Security = [Bearer];
        if (roles.Length > 0)
            operation.Description = Append(operation.Description, $"Vai trò: {string.Join(", ", roles)}.");
    }

    private static string Append(string? description, string line) =>
        string.IsNullOrEmpty(description) ? line : $"{description}\n\n{line}";
}
