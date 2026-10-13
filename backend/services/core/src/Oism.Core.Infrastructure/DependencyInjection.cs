using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Messaging;
using Oism.BuildingBlocks.Persistence;
using Oism.Core.Application;
using Oism.Core.Application.Inventory;
using Oism.Core.Application.Orders;
using Oism.Core.Application.References;
using Oism.Core.Infrastructure.Jobs;
using Oism.Core.Infrastructure.Repositories;

namespace Oism.Core.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOismDbContext<CoreDbContext>(configuration, CoreDbContext.Schema)
            .AddOismMessaging<CoreDbContext>()
            .AddSingleton<IClock, SystemClock>()
            // Thời hạn giữ hàng mặc định 30 phút (ADR-0005).
            .AddSingleton(new ReservationSettings(configuration.GetValue("Reservation:HoldMinutes", 30)))
            .AddScoped<IUnitOfWork, UnitOfWork>()
            .AddScoped<IEventPublisher, OutboxEventPublisher>()
            .AddScoped<IReferenceRepository, ReferenceRepository>()
            .AddScoped<IInventoryRepository, InventoryRepository>()
            .AddScoped<IPurchaseReceiptRepository, PurchaseReceiptRepository>()
            .AddScoped<IStockTransferRepository, StockTransferRepository>()
            .AddScoped<IStocktakeRepository, StocktakeRepository>()
            .AddScoped<IStockQueries, StockQueries>()
            .AddScoped<IStockService, StockService>()
            .AddScoped<IOrderRepository, OrderRepository>()
            .AddScoped<IOrderQueries, OrderQueries>()
            .AddSingleton<ExpireReservationsJob>();

        // `Jobs:Enabled = false` tắt Hangfire: test tích hợp tự gọi job để không tranh với lịch chạy mỗi phút.
        if (configuration.GetValue("Jobs:Enabled", true))
        {
            // Bảng của Hangfire nằm trong schema riêng, không có tenant_id (docs/design/data-model/core.md).
            services
                .AddHangfire(hangfire => hangfire.UsePostgreSqlStorage(
                    storage => storage.UseNpgsqlConnection(configuration.GetConnectionString("Oism")),
                    new PostgreSqlStorageOptions { SchemaName = $"hangfire_{CoreDbContext.Schema}" }))
                .AddHangfireServer()
                .AddHostedService<JobSchedule>();
        }

        return services;
    }

    public static Task MigrateDatabaseAsync(this IServiceProvider services) =>
        services.MigrateAsync<CoreDbContext>();
}
