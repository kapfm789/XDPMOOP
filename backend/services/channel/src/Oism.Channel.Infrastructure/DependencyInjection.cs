using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Messaging;
using Oism.BuildingBlocks.Persistence;
using Oism.Channel.Application;
using Oism.Channel.Application.Shops;
using Oism.Channel.Application.Simulator.SimulateBurst;
using Oism.Channel.Application.Webhooks;
using Oism.Channel.Infrastructure.Repositories;

namespace Oism.Channel.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOismDbContext<ChannelDbContext>(configuration, ChannelDbContext.Schema)
            .AddOismMessaging<ChannelDbContext>()
            .AddSingleton<IClock, SystemClock>()
            .AddScoped<IUnitOfWork, UnitOfWork>()
            .AddScoped<IEventPublisher, OutboxEventPublisher>()
            .AddScoped<IChannelShopRepository, ChannelShopRepository>()
            .AddScoped<IWebhookEventRepository, WebhookEventRepository>()
            // Mặc định là cổng service nghe trong container (docs/architecture/context-and-containers.md).
            .AddHttpClient<IWebhookSender, HttpWebhookSender>(client =>
                client.BaseAddress = new Uri(configuration["Simulator:WebhookBaseUrl"] ?? "http://localhost:8080/"));
        return services;
    }

    public static Task MigrateDatabaseAsync(this IServiceProvider services) =>
        services.MigrateAsync<ChannelDbContext>();
}
