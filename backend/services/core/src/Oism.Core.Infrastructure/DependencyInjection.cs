using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Messaging;
using Oism.BuildingBlocks.Persistence;
using Oism.Core.Application.References;
using Oism.Core.Infrastructure.Repositories;

namespace Oism.Core.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration) =>
        services
            .AddOismDbContext<CoreDbContext>(configuration, CoreDbContext.Schema)
            .AddOismMessaging<CoreDbContext>()
            .AddScoped<IReferenceRepository, ReferenceRepository>();

    public static Task MigrateDatabaseAsync(this IServiceProvider services) =>
        services.MigrateAsync<CoreDbContext>();
}
