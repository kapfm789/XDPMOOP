using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Persistence;

namespace Oism.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration) =>
        services.AddOismDbContext<IdentityDbContext>(configuration, IdentityDbContext.Schema);

    public static Task MigrateDatabaseAsync(this IServiceProvider services) =>
        services.MigrateAsync<IdentityDbContext>();
}
