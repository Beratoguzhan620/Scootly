using Confluent.Kafka;

namespace Scootly.Infrastructure.Streaming;

public sealed class TelemetryStreamConsumer : IDisposable
{
    private readonly IConsumer<string, string> _consumer;
    private const string TopicName = "telemetry-stream";

    public TelemetryStreamConsumer(string groupId)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = "localhost:9092",
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _consumer.Subscribe(TopicName);
    }

    public ConsumeResult<string, string>? ConsumeOnce(TimeSpan timeout)
    {
        return _consumer.Consume(timeout);
    }

    public void Dispose()
    {
        _consumer.Close();
        _consumer.Dispose();
    }
}