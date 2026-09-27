using Scootly.Domain.Common;

namespace Scootly.Domain.Pricing;

/// <summary>
/// Bir sürüşün ücretini hesaplayan tarife (64. gün — ücret hesabının tüketiciye taşınması).
/// </summary>
/// <remarks>
/// <para>
/// <b>Bu sürüm bilerek küçük.</b> Planın Faz 1'de istediği tam fiyatlandırma
/// modeli (<c>Money</c>, <c>IFareCalculator</c>, mesafe/süre stratejileri,
/// veritabanında tutulan ve yöneticinin değiştirebildiği tarifeler) main'de
/// henüz yok. 64. günün tüketicisinin tetikleyeceği bir hesap olmadan
/// "ücret hesabı arka plana taşındı" demenin anlamı olmazdı; bu yüzden tek bir
/// sabit tarife var. Teknik borç listesinde kayıtlı.
/// </para>
/// <para>
/// Ücret <b>başlayan her dakika</b> için alınıyor (yukarı yuvarlama): 61 saniyelik
/// bir sürüş 2 dakika sayılır. Sektörde yaygın olan bu; aşağı yuvarlamak, bir
/// dakikanın altındaki her sürüşü yalnızca açılış ücretine indirirdi.
/// </para>
/// </remarks>
public sealed record Tariff(decimal UnlockFee, decimal PerMinuteRate)
{
    /// <summary>
    /// Geçici standart tarife. Sayılar gerçek bir fiyatlandırma kararı DEĞİL,
    /// yalnızca hesap zincirinin uçtan uca çalıştığını görmek için.
    /// </summary>
    public static Tariff Standard { get; } = new(UnlockFee: 10m, PerMinuteRate: 2.5m);

    public decimal Calculate(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            throw new DomainException("Sürüş süresi negatif olamaz.");

        var baslayanDakika = (decimal)Math.Ceiling(duration.TotalMinutes);

        return decimal.Round(UnlockFee + (baslayanDakika * PerMinuteRate), 2, MidpointRounding.AwayFromZero);
    }
}
