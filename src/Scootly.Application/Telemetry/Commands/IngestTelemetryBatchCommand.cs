namespace Scootly.Application.Telemetry.Commands;

/// <summary>Tek bir cihaz ölçümü (alım ucunun taşıdığı ham veri).</summary>
public sealed record TelemetryReadingInput(
    string DeviceId,
    double Latitude,
    double Longitude,
    int BatteryPercentage,
    DateTime RecordedAt);

/// <summary>Bir istekte gelen ölçüm yığını (51. gün).</summary>
/// <remarks>
/// Toplu alım tesadüf değil: 200 cihaz beş saniyede bir tek tek gönderseydi
/// saniyede 40 ayrı HTTP isteği olurdu. Her isteğin TLS el sıkışması, kimlik
/// doğrulaması ve yetki kontrolü var — ölçümün kendisinden pahalı. Cihaz
/// birkaç ölçümü biriktirip tek istekte gönderiyor.
/// </remarks>
public sealed record IngestTelemetryBatchCommand(IReadOnlyList<TelemetryReadingInput> Readings)
{
    /// <summary>
    /// Bir istekte kabul edilen en fazla ölçüm sayısı.
    /// </summary>
    /// <remarks>
    /// Üst sınır bir güvenlik sınırı: sınırsız bir yığın, tek istekle
    /// sunucuyu keyfi büyüklükte bir listeyi belleğe almaya zorlardı.
    /// <c>PageRequest.MaxPageSize</c> ile aynı gerekçe.
    /// </remarks>
    public const int MaxBatchSize = 500;
}
