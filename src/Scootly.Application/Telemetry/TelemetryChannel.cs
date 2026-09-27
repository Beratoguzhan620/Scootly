using System.Threading.Channels;

namespace Scootly.Application.Telemetry;

/// <summary>Cihazdan gelen ham telemetri okuması (henüz doğrulanmış bir domain nesnesi değil).</summary>
public sealed record TelemetryReadingData(
    Guid VehicleId,
    double Latitude,
    double Longitude,
    int BatteryPercentage,
    DateTime RecordedAt);

/// <summary>
/// API ile telemetri tüketicisi arasındaki sınırlı bellek içi kuyruk.
/// Kuyruk dolduğunda kayıt sessizce düşürülmez; <see cref="TryWrite"/> false döner ve
/// istemciye geri basınç (503 + Retry-After) uygulanır.
/// </summary>
public sealed class TelemetryChannel
{
    public const int Capacity = 10_000;

    private readonly Channel<TelemetryReadingData> _channel = Channel.CreateBounded<TelemetryReadingData>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });

    private readonly Lock _writeLock = new();

    public ChannelReader<TelemetryReadingData> Reader => _channel.Reader;

    /// <summary>Okumaların tamamını ya hep birlikte kuyruğa alır ya da hiçbirini almaz.</summary>
    public bool TryWriteBatch(IReadOnlyCollection<TelemetryReadingData> readings)
    {
        lock (_writeLock)
        {
            // Tek okuyucu yalnızca boşaltabildiği için, kilit altında kontrol edilen boş yer yazma bitene kadar küçülmez.
            if (Capacity - _channel.Reader.Count < readings.Count)
                return false;

            foreach (var reading in readings)
                _channel.Writer.TryWrite(reading);

            return true;
        }
    }
}
