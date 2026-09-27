using System.Text.Json;
using Confluent.Kafka;

namespace Scootly.Concurrency.Tests.Experiments.Streaming;

/// <summary>
/// ADR 0016 deneyi: log tabanlı stream (Redpanda/Kafka) istemcileri. Üretim mimarisine entegre edilmedi;
/// bu yüzden üretim kodunda değil, yalnızca deney testinin yanında durur.
/// </summary>
public sealed record StreamedTelemetry(Guid VehicleId, double Latitude, double Longitude, int BatteryPercentage, DateTime RecordedAt);

public sealed class TelemetryStreamProducer : IDisposable
{
    public const string TopicName = "telemetry-stream";

    private readonly IProducer<string, string> _producer;

    public TelemetryStreamProducer(string bootstrapServers)
    {
        _producer = new ProducerBuilder<string, string>(new ProducerConfig { BootstrapServers = bootstrapServers }).Build();
    }

    public Task PublishAsync(StreamedTelemetry reading, CancellationToken cancellationToken = default)
        => _producer.ProduceAsync(TopicName, new Message<string, string>
        {
            Key = reading.VehicleId.ToString(),
            Value = JsonSerializer.Serialize(reading)
        }, cancellationToken);

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}

public sealed class TelemetryStreamConsumer : IDisposable
{
    private readonly IConsumer<string, string> _consumer;

    public TelemetryStreamConsumer(string bootstrapServers, string groupId)
    {
        _consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest
        }).Build();

        _consumer.Subscribe(TelemetryStreamProducer.TopicName);
    }

    public ConsumeResult<string, string>? ConsumeOnce(TimeSpan timeout) => _consumer.Consume(timeout);

    public void Dispose()
    {
        _consumer.Close();
        _consumer.Dispose();
    }
}
