using System.Text;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Scootly.Infrastructure.Messaging;
using Scootly.Infrastructure.Messaging.Outbox;
using Scootly.Infrastructure.Persistence;

namespace Scootly.Api.Services;

public sealed class OutboxPublisher : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly RabbitMqConnectionProvider _connectionProvider;
    private readonly ILogger<OutboxPublisher> _logger;
    private const string ExchangeName = "scootly.events";

    public OutboxPublisher(
        IServiceProvider services,
        RabbitMqConnectionProvider connectionProvider,
        ILogger<OutboxPublisher> logger)
    {
        _services = services;
        _connectionProvider = connectionProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

                var unprocessedMessages = dbContext.Set<OutboxMessage>()
                    .Where(m => m.ProcessedAt == null)
                    .OrderBy(m => m.CreatedAt)
                    .Take(50)
                    .ToList();

                if (unprocessedMessages.Count > 0)
                {
                    var connection = await _connectionProvider.GetConnectionAsync();
                    using var channel = await connection.CreateChannelAsync();

                    await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Topic, durable: true);

                    foreach (var message in unprocessedMessages)
                    {
                        var body = Encoding.UTF8.GetBytes(message.Payload);
                        var routingKey = message.EventType;

                        await channel.BasicPublishAsync(ExchangeName, routingKey, body, stoppingToken);

                        message.MarkAsProcessed(DateTime.UtcNow);

                        _logger.LogInformation("Outbox mesajı yayınlandı: {EventType} ({MessageId})", message.EventType, message.Id);
                    }

                    await dbContext.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Uygulama kapanıyor, normal.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxPublisher turunda beklenmeyen hata oluştu.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}