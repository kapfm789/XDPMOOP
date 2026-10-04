using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace Oism.BuildingBlocks.Messaging;

internal static class RabbitMq
{
    public const string Exchange = "oism.events";

    // URI dạng amqp://user:password@host:5672. Bỏ trống thì service không gửi và không nhận thông điệp.
    public static string? Uri(IConfiguration configuration) => configuration.GetConnectionString("RabbitMq");

    public static Task<IConnection> ConnectAsync(IConfiguration configuration, bool automaticRecovery, CancellationToken ct) =>
        new ConnectionFactory { Uri = new Uri(Uri(configuration)!), AutomaticRecoveryEnabled = automaticRecovery }
            .CreateConnectionAsync(ct);

    public static Task DeclareExchangeAsync(IChannel channel, CancellationToken ct) =>
        channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
}
