using Npgsql;
using NpgsqlTypes;
using Scootly.Application.Abstractions;
using Scootly.Domain.Telemetry;
using Scootly.Infrastructure.Persistence.Configurations;

namespace Scootly.Infrastructure.Persistence;

/// <summary>Toplu telemetri yazımı için bağlantı ayarı.</summary>
public sealed record TelemetryWriteOptions(string ConnectionString);

/// <summary>
/// Telemetriyi PostgreSQL'in <c>COPY</c> protokolüyle yazar (44. gün).
/// </summary>
/// <remarks>
/// <para>
/// <b>Neden EF Core değil.</b> <c>AddRange</c> + <c>SaveChanges</c> on bin
/// kayıt için on bin parametreli INSERT ifadeleri üretir; EF bunları gruplar
/// ama her grup hâlâ ayrıştırılıp planlanır ve her satır önce değişiklik
/// izleyicisine girer. <c>COPY</c> bu adımların hiçbirini yapmaz: satırlar
/// ikili biçimde tek akışta sunucuya gider.
/// </para>
/// <para>
/// <b>Ödenen bedel dürüstçe:</b> bu sınıf tablo ve sütun adlarını METİN olarak
/// biliyor. Şema değişirse derleme hatası vermez, çalışma zamanında patlar.
/// Bu yüzden tablo adı <see cref="TelemetryReadingConfiguration.TabloAdi"/>
/// sabitinden geliyor ve sütun adları <c>Gun44_TopluYazmaTests</c> ile
/// doğrulanıyor — ORM'i atlamanın bedeli, onun verdiği derleme zamanı
/// güvencesini kaybetmek.
/// </para>
/// <para>
/// <b>Kendi bağlantısını açıyor</b>, <c>DbContext</c> paylaşmıyor. Sebebi:
/// bu yazıcı arka plan servisinden çağrılıyor ve <c>DbContext</c> iş parçacığı
/// güvenli değil. Kapsamlı (scoped) bir bağlamı arka plan servisine enjekte
/// etmek, 55. günün "yaygın tuzaklar" listesindeki ilk madde.
/// </para>
/// </remarks>
public sealed class TelemetryBulkWriter : ITelemetryWriter
{
    private static readonly string CopyKomutu =
        $"COPY \"{TelemetryReadingConfiguration.TabloAdi}\" " +
        "(\"Id\", \"DeviceId\", \"Latitude\", \"Longitude\", \"BatteryPercentage\", \"RecordedAt\") " +
        "FROM STDIN (FORMAT BINARY)";

    private readonly TelemetryWriteOptions _options;

    public TelemetryBulkWriter(TelemetryWriteOptions options)
    {
        _options = options;
    }

    public async Task<int> WriteBatchAsync(
        IReadOnlyCollection<TelemetryReading> readings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(readings);

        if (readings.Count == 0)
        {
            return 0;
        }

        await using var baglanti = new NpgsqlConnection(_options.ConnectionString);
        await baglanti.OpenAsync(cancellationToken);

        await using var yazici = await baglanti.BeginBinaryImportAsync(CopyKomutu, cancellationToken);

        foreach (var okuma in readings)
        {
            await yazici.StartRowAsync(cancellationToken);

            await yazici.WriteAsync(okuma.Id, NpgsqlDbType.Uuid, cancellationToken);
            await yazici.WriteAsync(okuma.DeviceId.Value, NpgsqlDbType.Varchar, cancellationToken);
            await yazici.WriteAsync(okuma.Location.Latitude, NpgsqlDbType.Double, cancellationToken);
            await yazici.WriteAsync(okuma.Location.Longitude, NpgsqlDbType.Double, cancellationToken);
            await yazici.WriteAsync(okuma.BatteryPercentage, NpgsqlDbType.Integer, cancellationToken);
            await yazici.WriteAsync(okuma.RecordedAt, NpgsqlDbType.TimestampTz, cancellationToken);
        }

        // CompleteAsync çağrılmazsa akış iptal edilir ve HİÇBİR SATIR yazılmaz.
        // Sessiz değil — ama "using bloğu bitti, yazılmıştır" varsayımı yanlış.
        var yazilan = await yazici.CompleteAsync(cancellationToken);

        return (int)yazilan;
    }
}
