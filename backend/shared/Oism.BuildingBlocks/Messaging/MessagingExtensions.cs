using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Oism.BuildingBlocks.Persistence;

namespace Oism.BuildingBlocks.Messaging;

public static class MessagingExtensions
{
    // Gọi ở Infrastructure của service có phát hoặc nhận thông điệp. DbContext cài IHasOutbox thì có tiến trình
    // đẩy outbox; cài IHasInbox thì nhận thông điệp cho các consumer đăng ký bằng AddEventConsumer.
    public static IServiceCollection AddOismMessaging<TContext>(this IServiceCollection services)
        where TContext : OismDbContext
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<OismDbContext>(provider => provider.GetRequiredService<TContext>());

        if (typeof(IHasOutbox).IsAssignableFrom(typeof(TContext)))
            services.AddHostedService<OutboxDispatcher>();
        if (typeof(IHasInbox).IsAssignableFrom(typeof(TContext)))
            services.AddHostedService<EventConsumerHost>();

        return services;
    }

    // Gọi ở Program.cs của Api. Queue của consumer là <service>.<tên TPayload>, ví dụ core.SkuUpserted.
    public static IServiceCollection AddEventConsumer<TConsumer, TPayload>(this IServiceCollection services)
        where TConsumer : EventConsumer<TPayload>
    {
        services.AddScoped<TConsumer>();
        return services.AddSingleton(new ConsumerRegistration(typeof(TPayload).Name, (provider, envelope, ct) =>
            provider.GetRequiredService<TConsumer>().HandleAsync(
                envelope.Payload.Deserialize<TPayload>(EventEnvelope.JsonOptions)!, ct)));
    }
}
