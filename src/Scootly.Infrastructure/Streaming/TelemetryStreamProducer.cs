using System.Text.Json;
using Confluent.Kafka;
using Scootly.Domain.Telemetry;

namespace Scootly.Infrastructure.Streaming;

public sealed class TelemetryStreamProducer : IAsyncDisposable
{
    private readonly IProducer<string, string> _producer;
    private const string TopicName = "telemetry-stream";

    public TelemetryStreamProducer()
    {
        var config = new ProducerConfig { BootstrapServers = "localhost:9092" };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync(TelemetryReading reading, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            reading.VehicleId,
            reading.Location.Latitude,
            reading.Location.Longitude,
            reading.BatteryPercentage,
            reading.RecordedAt
        });

        await _producer.ProduceAsync(TopicName, new Message<string, string>
        {
            Key = reading.VehicleId.ToString(),
            Value = payload
        }, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
        return ValueTask.CompletedTask;
    }
}