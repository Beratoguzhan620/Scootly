using System.Threading.Channels;
using Scootly.Domain.Telemetry;

namespace Scootly.Application.Telemetry;

public sealed class TelemetryChannel
{
    private readonly Channel<TelemetryReading> _channel;

    public TelemetryChannel()
    {
        _channel = Channel.CreateBounded<TelemetryReading>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    public ValueTask WriteAsync(TelemetryReading reading, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(reading, cancellationToken);

    public IAsyncEnumerable<TelemetryReading> ReadAllAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}