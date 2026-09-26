using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Scootly.Application.IntegrationEvents;
using Scootly.Infrastructure.Messaging;

namespace Scootly.Worker.Jobs;

public sealed class BatteryLowConsumer : BackgroundService
{
    private readonly RabbitMqConnectionProvider _connectionProvider;
    private readonly ILogger<BatteryLowConsumer> _logger;
    private const string ExchangeName = "scootly.events";
    private const string QueueName = "scootly.battery-low-consumer";

    public BatteryLowConsumer(RabbitMqConnectionProvider connectionProvider, ILogger<BatteryLowConsumer> logger)
    {
        _connectionProvider = connectionProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var connection = await _connectionProvider.GetConnectionAsync();
            var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);
            await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
            await channel.QueueBindAsync(QueueName, ExchangeName, "VehicleBatteryLow", cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (sender, eventArgs) =>
            {
                try
                {
                    var body = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
                    var integrationEvent = JsonSerializer.Deserialize<VehicleBatteryLowIntegrationEvent>(body);

                    if (integrationEvent is not null)
                    {
                        _logger.LogWarning(
                            "Saha görevi oluşturulmalı — Batarya düşük: Vehicle={VehicleId}, %{BatteryPercentage}",
                            integrationEvent.VehicleId, integrationEvent.BatteryPercentage);
                    }

                    await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "BatteryLow mesajı işlenirken hata oluştu.");
                    await channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: false);
                }
            };

            await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer, stoppingToken);
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BatteryLowConsumer başlatılırken hata oluştu.");
        }
    }
}