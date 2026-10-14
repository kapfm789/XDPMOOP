using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Messaging;
using Oism.BuildingBlocks.Persistence;
using Oism.Insights.Application;
using Oism.Insights.Application.Projections;
using Oism.Insights.Application.Reports;
using Oism.Insights.Infrastructure.Jobs;
using Oism.Insights.Infrastructure.Repositories;

namespace Oism.Insights.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOismDbContext<InsightsDbContext>(configuration, InsightsDbContext.Schema)
            .AddOismMessaging<InsightsDbContext>()
            .AddSingleton<IClock, SystemClock>()
            .AddScoped<IUnitOfWork, UnitOfWork>()
            .AddScoped<IProjectionRepository, ProjectionRepository>()
            .AddScoped<IDailyAggregateRepository, DailyAggregateRepository>()
            .AddScoped<IReportQueries, ReportQueries>()
            .AddSingleton<DailyAggregationJob>();

        // `Jobs:Enabled = false` tắt Hangfire: test tích hợp tự gọi job.
        if (configuration.GetValue("Jobs:Enabled", true))
        {
            // Bảng của Hangfire nằm trong schema riêng, không có tenant_id (docs/design/data-model/insights.md).
            services
                .AddHangfire(hangfire => hangfire.UsePostgreSqlStorage(
                    storage => storage.UseNpgsqlConnection(configuration.GetConnectionString("Oism")),
                    new PostgreSqlStorageOptions { SchemaName = $"hangfire_{InsightsDbContext.Schema}" }))
                .AddHangfireServer()
                .AddHostedService<JobSchedule>();
        }

        return services;
    }

    public static Task MigrateDatabaseAsync(this IServiceProvider services) =>
        services.MigrateAsync<InsightsDbContext>();
}
