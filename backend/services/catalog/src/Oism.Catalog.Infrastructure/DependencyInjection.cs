using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Persistence;

namespace Oism.Catalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration) =>
        services.AddOismDbContext<CatalogDbContext>(configuration, CatalogDbContext.Schema);

    public static Task MigrateDatabaseAsync(this IServiceProvider services) =>
        services.MigrateAsync<CatalogDbContext>();
}
