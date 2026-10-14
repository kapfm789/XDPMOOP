using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Messaging;
using Oism.BuildingBlocks.Persistence;
using Oism.Insights.Application;
using Oism.Insights.Application.Projections;
using Oism.Insights.Infrastructure.Repositories;

namespace Oism.Insights.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration) =>
        services
            .AddOismDbContext<InsightsDbContext>(configuration, InsightsDbContext.Schema)
            .AddOismMessaging<InsightsDbContext>()
            .AddSingleton<IClock, SystemClock>()
            .AddScoped<IProjectionRepository, ProjectionRepository>();

    public static Task MigrateDatabaseAsync(this IServiceProvider services) =>
        services.MigrateAsync<InsightsDbContext>();
}

internal sealed class SystemClock : IClock
{
    // PostgreSQL giữ thời gian tới micro giây. Cắt ngay ở đây để giá trị trả về lúc tạo bằng đúng giá trị đọc lại sau đó.
    public DateTimeOffset UtcNow
    {
        get
        {
            var now = DateTimeOffset.UtcNow;
            return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
        }
    }
}
