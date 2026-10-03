using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Persistence;

namespace Oism.Core.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration) =>
        services.AddOismDbContext<CoreDbContext>(configuration, CoreDbContext.Schema);

    public static Task MigrateDatabaseAsync(this IServiceProvider services) =>
        services.MigrateAsync<CoreDbContext>();
}
