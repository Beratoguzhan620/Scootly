using Scootly.Domain.Common;
using Scootly.Domain.Geo;

namespace Scootly.Domain.Telemetry;

/// <summary>
/// Bir cihazın tek bir anlık bildirimi: neredeydi, bataryası kaçtı, ne zaman.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="AggregateRoot"/>'tan DEĞİL, <see cref="Entity"/>'den türüyor.
/// Fark önemli: toplam kök, etrafında iş kuralları ve değişmezlikler (invariant)
/// toplanan bir kavramdır ve alan olayları yayar. Telemetri kaydı ise
/// değiştirilmeyen, yalnızca eklenen bir ölçüm — saniyede yüzlercesi gelir ve
/// her biri için olay listesi tutmak saf israf olurdu.
/// </para>
/// <para>
/// <b>Kayıt değiştirilemez.</b> Bütün alanlar salt okunur ve hiçbir metot
/// durumu değiştirmiyor. Bir ölçümü sonradan düzeltmek, ölçüm olmaktan
/// çıkarır; yanlışsa yeni bir kayıt gelir.
/// </para>
/// <para>
/// Telemetri <c>Vehicles</c> tablosuna değil kendi tablosuna yazılıyor —
/// Karar 3'ün pratiğe dökülmesi. Aynı satıra yazılsaydı saniyede yüzlerce
/// güncelleme, kiralama işlemlerinin ihtiyaç duyduğu satırları kilitler ve
/// 38. günde kurduğumuz sürüm damgası her kiralamada çakışma üretirdi.
/// </para>
/// </remarks>
public sealed class TelemetryReading : Entity
{
    public DeviceId DeviceId { get; }
    public GeoPoint Location { get; }
    public int BatteryPercentage { get; }
    public DateTime RecordedAt { get; }

    private TelemetryReading()
    {
        DeviceId = null!;
        Location = null!;
    }

    public TelemetryReading(
        Guid id,
        DeviceId deviceId,
        GeoPoint location,
        int batteryPercentage,
        DateTime recordedAt)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        ArgumentNullException.ThrowIfNull(location);

        if (batteryPercentage is < 0 or > 100)
        {
            throw new DomainException("Batarya yüzdesi 0-100 arasında olmalı.");
        }

        if (recordedAt.Kind != DateTimeKind.Utc)
        {
            // Cihazlar dünyanın her yerinde olabilir. Yerel saatli bir damga,
            // iki cihazın ölçümünü sıralanamaz hale getirir ve yaz saati
            // geçişinde aynı saat iki kez yaşanır. PostgreSQL sütunu da
            // "timestamp with time zone" — tutarsız bir Kind sessizce
            // kaydırılmış zaman üretirdi.
            throw new DomainException("Ölçüm zamanı UTC olmalı.");
        }

        DeviceId = deviceId;
        Location = location;
        BatteryPercentage = batteryPercentage;
        RecordedAt = recordedAt;
    }
}
