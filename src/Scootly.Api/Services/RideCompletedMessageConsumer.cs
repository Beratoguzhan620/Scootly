using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Scootly.Application.IntegrationEvents;
using Scootly.Infrastructure.Messaging;
using Scootly.Infrastructure.Messaging.Idempotency;
using Scootly.Infrastructure.Persistence;

namespace Scootly.Api.Services;

public sealed class RideCompletedMessageConsumer : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly RabbitMqConnectionProvider _connectionProvider;
    private readonly ILogger<RideCompletedMessageConsumer> _logger;
    private const string ExchangeName = "scootly.events";
    private const string QueueName = "scootly.ride-completed-consumer";
    private const string DeadLetterExchangeName = "scootly.events.dlx";
    private const string DeadLetterQueueName = "scootly.ride-completed-consumer.dlq";
    private const int MaxRetryCount = 3;

    public RideCompletedMessageConsumer(
        IServiceProvider services,
        RabbitMqConnectionProvider connectionProvider,
        ILogger<RideCompletedMessageConsumer> logger)
    {
        _services = services;
        _connectionProvider = connectionProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var connection = await _connectionProvider.GetConnectionAsync();
            var channel = await connection.CreateChannelAsync();

            await channel.ExchangeDeclareAsync(DeadLetterExchangeName, ExchangeType.Fanout, durable: true, cancellationToken: stoppingToken);
            await channel.QueueDeclareAsync(DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
            await channel.QueueBindAsync(DeadLetterQueueName, DeadLetterExchangeName, string.Empty, cancellationToken: stoppingToken);

            await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);

            var queueArgs = new Dictionary<string, object?>
            {
                { "x-dead-letter-exchange", DeadLetterExchangeName }
            };

            await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false, arguments: queueArgs, cancellationToken: stoppingToken);
            await channel.QueueBindAsync(QueueName, ExchangeName, "RideCompleted", cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (sender, eventArgs) =>
            {
                try
                {
                    var body = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
                    var integrationEvent = JsonSerializer.Deserialize<RideCompletedIntegrationEvent>(body);

                    if (integrationEvent is null)
                    {
                        await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
                        return;
                    }

                    using var scope = _services.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();
                    var idempotentHandler = new IdempotentMessageHandler(dbContext);

                    var processed = await idempotentHandler.TryProcessAsync(integrationEvent.RideId, async () =>
                    {
                        _logger.LogInformation(
                            "RideCompleted mesajı işlendi: Ride={RideId}, Süre={DurationMinutes}dk, Mesafe={DistanceMeters}m",
                            integrationEvent.RideId, integrationEvent.DurationMinutes, integrationEvent.DistanceMeters);

                        await Task.CompletedTask;
                    });

                    if (!processed)
                    {
                        _logger.LogInformation("RideCompleted mesajı zaten işlenmişti, atlandı: {RideId}", integrationEvent.RideId);
                    }

                    await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    var retryCount = GetRetryCount(eventArgs.BasicProperties.Headers);

                    if (retryCount >= MaxRetryCount)
                    {
                        _logger.LogError(ex,
                            "RideCompleted mesajı {RetryCount} kez denendi ve başarısız oldu, ölü mektup kuyruğuna gönderiliyor.",
                            retryCount);

                        await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
                    }
                    else
                    {
                        _logger.LogWarning(ex,
                            "RideCompleted mesajı işlenirken hata oluştu, yeniden denenecek ({RetryCount}/{MaxRetryCount}).",
                            retryCount + 1, MaxRetryCount);

                        await channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: false);
                    }
                }
            };

            await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer, stoppingToken);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Uygulama kapanıyor, normal.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RideCompletedMessageConsumer başlatılırken hata oluştu. Servis bu turda devre dışı kalacak.");
        }
    }

    private static int GetRetryCount(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue("x-death", out var xDeath) || xDeath is not List<object> deathList)
            return 0;

        return deathList.Count;
    }
}