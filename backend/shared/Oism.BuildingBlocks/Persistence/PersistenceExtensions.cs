using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Oism.BuildingBlocks.Persistence;

public static class PersistenceExtensions
{
    // Mọi service dùng chung chuỗi kết nối tới database oism và chỉ đọc ghi schema của mình (ADR-0013).
    public static IServiceCollection AddOismDbContext<TContext>(
        this IServiceCollection services, IConfiguration configuration, string schema)
        where TContext : OismDbContext =>
        services.AddDbContext<TContext>(options => options
            .UseNpgsql(
                configuration.GetConnectionString("Oism"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema))
            .UseSnakeCaseNamingConvention());

    public static async Task MigrateAsync<TContext>(this IServiceProvider services)
        where TContext : OismDbContext
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TContext>().Database.MigrateAsync();
    }
}
