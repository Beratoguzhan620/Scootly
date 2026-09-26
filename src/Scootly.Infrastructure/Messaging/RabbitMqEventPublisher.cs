using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Scootly.Application.Abstractions;

namespace Scootly.Infrastructure.Messaging;

public sealed class RabbitMqEventPublisher : IEventPublisher
{
    private readonly RabbitMqConnectionProvider _connectionProvider;
    private const string ExchangeName = "scootly.events";

    public RabbitMqEventPublisher(RabbitMqConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public async Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
    {
        var connection = await _connectionProvider.GetConnectionAsync();
        using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Topic, durable: true, cancellationToken: cancellationToken);

        var routingKey = typeof(T).Name.Replace("IntegrationEvent", string.Empty);
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(integrationEvent));

        var properties = new BasicProperties { Persistent = true };

        await channel.BasicPublishAsync(ExchangeName, routingKey, false, properties, body, cancellationToken);
    }
}