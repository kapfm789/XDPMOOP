using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Messaging;
using Oism.BuildingBlocks.Persistence;
using Oism.Core.Application;
using Oism.Core.Application.Inventory;
using Oism.Core.Application.Orders;
using Oism.Core.Application.References;
using Oism.Core.Infrastructure.Repositories;

namespace Oism.Core.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration) =>
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
            .AddScoped<IStockQueries, StockQueries>()
            .AddScoped<IStockService, StockService>()
            .AddScoped<IOrderRepository, OrderRepository>();

    public static Task MigrateDatabaseAsync(this IServiceProvider services) =>
        services.MigrateAsync<CoreDbContext>();
}
