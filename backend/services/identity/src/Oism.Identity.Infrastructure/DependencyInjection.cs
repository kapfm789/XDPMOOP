using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Messaging;
using Oism.BuildingBlocks.Persistence;
using Oism.Identity.Application;
using Oism.Identity.Application.Auth;
using Oism.Identity.Application.Branches;
using Oism.Identity.Application.Tenants;
using Oism.Identity.Application.Users;
using Oism.Identity.Infrastructure.Repositories;
using Oism.Identity.Infrastructure.Security;

namespace Oism.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration) =>
        services
            .AddOismDbContext<IdentityDbContext>(configuration, IdentityDbContext.Schema)
            .AddOismMessaging<IdentityDbContext>()
            .AddScoped<IUnitOfWork, UnitOfWork>()
            .AddScoped<IEventPublisher, OutboxEventPublisher>()
            .AddScoped<ITenantRepository, TenantRepository>()
            .AddScoped<IUserRepository, UserRepository>()
            .AddScoped<IRefreshTokenRepository, RefreshTokenRepository>()
            .AddScoped<IBranchRepository, BranchRepository>()
            .AddSingleton<IClock, SystemClock>()
            .AddSingleton<IPasswordHasher, BCryptPasswordHasher>()
            .AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

    public static Task MigrateDatabaseAsync(this IServiceProvider services) =>
        services.MigrateAsync<IdentityDbContext>();
}
