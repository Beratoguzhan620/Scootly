using Scootly.Domain.Common;

namespace Scootly.Domain.Pricing;

/// <summary>
/// Bir sürüşün nasıl ücretlendirileceği: kilit açma bedeli + dakika ücreti.
/// </summary>
/// <remarks>
/// <para>
/// Faz 1'den beri teknik borç listesinde duran iki tipten biri (diğeri
/// <c>Wallet</c>, o hâlâ yok). 46. gün önbelleklemeye bir tarife sorgusuyla
/// başlamayı söylüyor ve gerekçesi iyi: tarife, önbelleklenmesi <b>en düşük
/// riskli</b> veri türü — nadiren değişir ve herkes için aynıdır. Kullanıcıya
/// özel veriyle başlamak, ilk günden "yanlış kullanıcıya yanlış veri" hatasına
/// davetiye çıkarırdı.
/// </para>
/// <para>
/// <b>Aynı anda yalnızca bir tarife aktif olabilir.</b> Bu kural veritabanında
/// kısmi tekil (partial unique) indeksle zorlanıyor, sadece kodda değil —
/// iki aktif tarifenin var olduğu bir an, iki sürüşün farklı fiyatlandığı bir
/// an demek.
/// </para>
/// </remarks>
public sealed class Tariff : Entity
{
    public const int MaxNameLength = 64;

    public string Name { get; private set; }
    public Money UnlockFee { get; private set; }
    public Money PerMinuteFee { get; private set; }
    public bool IsActive { get; private set; }

    private Tariff()
    {
        Name = string.Empty;
        UnlockFee = null!;
        PerMinuteFee = null!;
    }

    public Tariff(Guid id, string name, Money unlockFee, Money perMinuteFee)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(unlockFee);
        ArgumentNullException.ThrowIfNull(perMinuteFee);

        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
        {
            throw new DomainException($"Tarife adı boş olamaz ve {MaxNameLength} karakteri aşamaz.");
        }

        if (!string.Equals(unlockFee.Currency, perMinuteFee.Currency, StringComparison.Ordinal))
        {
            throw new DomainException("Tarifenin iki bileşeni aynı para biriminde olmalı.");
        }

        Name = name;
        UnlockFee = unlockFee;
        PerMinuteFee = perMinuteFee;
        IsActive = true;
    }

    /// <summary>Verilen süre için ücreti hesaplar.</summary>
    /// <remarks>
    /// Başlanan dakika tam sayılıyor (yukarı yuvarlama). Bu bir iş kuralı,
    /// hesaplama detayı değil: kullanıcıya "31 saniye sürdün, bir dakika
    /// ödüyorsun" demek açıklanabilir; saniyenin küsuratını faturaya yazmak
    /// değil.
    /// </remarks>
    public Money CalculateFare(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new DomainException("Sürüş süresi negatif olamaz.");
        }

        var baslananDakika = (decimal)Math.Ceiling(duration.TotalMinutes);

        return UnlockFee.Plus(PerMinuteFee.Times(baslananDakika));
    }

    public void Deactivate() => IsActive = false;
}
