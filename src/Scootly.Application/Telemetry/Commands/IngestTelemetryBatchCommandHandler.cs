using Scootly.Domain.Common;
using Scootly.Domain.Geo;
using Scootly.Domain.Telemetry;

namespace Scootly.Application.Telemetry.Commands;

/// <summary>
/// Gelen telemetri yığınını doğrular ve kuyruğa koyar (51-52. gün).
/// </summary>
/// <remarks>
/// <para>
/// Bu handler veritabanına HİÇ GİTMİYOR. 51. günde doğrudan yazıyordu;
/// 52. günde kanal araya girdi. Fark ölçülebilir: yanıt süresi artık yazma
/// hızından bağımsız, milisaniyeler mertebesinde.
/// </para>
/// <para>
/// <b>Doğrulama kuyruktan ÖNCE.</b> Geçersiz bir ölçüm kuyruğa girseydi, hata
/// arka plan servisinde ortaya çıkardı — yani isteği gönderen cihaz 202
/// yanıtını çoktan almış olurdu ve hatayı kimse ona söyleyemezdi. Alan modeli
/// nesnesini burada kurmak, geçersiz verinin sınırı geçememesini sağlıyor.
/// </para>
/// </remarks>
public sealed class IngestTelemetryBatchCommandHandler
{
    private readonly TelemetryChannel _channel;

    public IngestTelemetryBatchCommandHandler(TelemetryChannel channel)
    {
        _channel = channel;
    }

    public Result<IngestSonucu> Handle(IngestTelemetryBatchCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Readings.Count == 0)
        {
            return Result<IngestSonucu>.Failure("Yığın boş olamaz.");
        }

        if (command.Readings.Count > IngestTelemetryBatchCommand.MaxBatchSize)
        {
            return Result<IngestSonucu>.Failure(
                $"Bir istekte en fazla {IngestTelemetryBatchCommand.MaxBatchSize} ölçüm gönderilebilir.");
        }

        var olcumler = new List<TelemetryReading>(command.Readings.Count);

        foreach (var girdi in command.Readings)
        {
            try
            {
                olcumler.Add(new TelemetryReading(
                    Guid.NewGuid(),
                    new DeviceId(girdi.DeviceId),
                    new GeoPoint(girdi.Latitude, girdi.Longitude),
                    girdi.BatteryPercentage,
                    DateTime.SpecifyKind(girdi.RecordedAt, DateTimeKind.Utc)));
            }
            catch (DomainException ex)
            {
                // TEK BİR geçersiz ölçüm bütün yığını reddediyor. Alternatif,
                // geçerli olanları kabul edip gerisini atmaktı; onu seçmedik
                // çünkü kısmi başarı cihaz tarafında "hangileri geçti"
                // sorusunu doğuruyor ve cevabı yok. Yığın atomik.
                return Result<IngestSonucu>.Failure($"Geçersiz ölçüm: {ex.Message}");
            }
        }

        var atilan = _channel.Yaz(olcumler);

        return Result<IngestSonucu>.Success(new IngestSonucu(olcumler.Count - atilan, atilan));
    }
}

/// <summary>Kaçı kuyruğa alındı, kaçı atıldı.</summary>
/// <remarks>
/// Atılan sayısı yanıtta dönüyor. Cihaz bunu görüp gönderim sıklığını
/// azaltabilir — ve daha önemlisi, veri kaybı sessiz kalmıyor.
/// </remarks>
public sealed record IngestSonucu(int KuyrugaAlinan, int Atilan);
