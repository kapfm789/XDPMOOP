using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Persistence;
using Oism.Catalog.Application;
using Oism.Catalog.Application.Brands;
using Oism.Catalog.Application.Categories;
using Oism.Catalog.Infrastructure.Repositories;

namespace Oism.Catalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration) =>
        services
            .AddOismDbContext<CatalogDbContext>(configuration, CatalogDbContext.Schema)
            .AddScoped<IUnitOfWork, UnitOfWork>()
            .AddScoped<ICategoryRepository, CategoryRepository>()
            .AddScoped<IBrandRepository, BrandRepository>();

    public static Task MigrateDatabaseAsync(this IServiceProvider services) =>
        services.MigrateAsync<CatalogDbContext>();
}
