using Scootly.Domain.Telemetry;

namespace Scootly.Application.Abstractions;

/// <summary>
/// Telemetri kayıtlarını TOPLU olarak yazar (44. gün).
/// </summary>
/// <remarks>
/// <para>
/// Arayüz tek tek değil, koleksiyon alıyor — ve bu bilinçli bir kısıt. Tek
/// kayıt alan bir metot olsaydı, çağıran taraf onu bir döngü içinde çağırır ve
/// saniyede yüzlerce tekil <c>INSERT</c> üretirdi. Her INSERT'ün kendi ağ gidiş
/// dönüşü, kendi ayrıştırma ve planlama maliyeti, kendi örtük transaction'ı
/// var. Toplu yazmayı mümkün kılmak yeterli değil; <b>tekil yazmayı imkânsız
/// kılmak</b> gerekiyordu.
/// </para>
/// <para>
/// Uygulaması PostgreSQL'in <c>COPY</c> protokolünü kullanıyor: satırlar ikili
/// (binary) biçimde tek akışta gönderiliyor, SQL ayrıştırma adımı hiç
/// çalışmıyor. Ölçümü 44. günün tablosunda.
/// </para>
/// </remarks>
public interface ITelemetryWriter
{
    /// <summary>Yazılan satır sayısını döndürür.</summary>
    Task<int> WriteBatchAsync(
        IReadOnlyCollection<TelemetryReading> readings,
        CancellationToken cancellationToken = default);
}
