namespace Scootly.Application.IntegrationEvents;

/// <summary>Bir mesaj işlenemediğinde ne yapılacağı.</summary>
public enum DeliveryOutcome
{
    /// <summary>Kısa bir beklemeden sonra aynı tüketiciye tekrar ver.</summary>
    Retry,

    /// <summary>Ana akıştan çıkar, ölü mektup kuyruğunda incelenmek üzere beklet.</summary>
    DeadLetter
}

/// <summary>Başarısızlığın türü.</summary>
public enum FailureKind
{
    /// <summary>
    /// Mesaj okunamıyor ya da anlamsız (zehirli mesaj). Tekrar denemek aynı
    /// sonucu verir.
    /// </summary>
    Poison,

    /// <summary>
    /// Mesaj geçerli ama iş kuralı reddetti — örneğin sürüş bulunamadı.
    /// Tekrar denemek aynı sonucu verir.
    /// </summary>
    Rejected,

    /// <summary>
    /// Beklenmeyen bir istisna: veritabanı kısa süreliğine erişilemedi, zaman
    /// aşımı, iki tüketicinin aynı anda yazmaya çalışması. Birkaç saniye sonra
    /// geçebilir.
    /// </summary>
    Transient
}

/// <summary>
/// Yeniden deneme ve ölü mektup kuralı (65. gün).
/// </summary>
/// <remarks>
/// <para>
/// Kural taşıma katmanından (RabbitMQ) bağımsız olsun diye burada: "bir mesaj
/// kaç kez denenmeli ve sonra ne olmalı" planın kendi sorusu ve cevabı bir
/// kütüphane ayarı değil, bir karar. Burada olması onu kuyruğa bağlanmadan
/// test edilebilir kılıyor.
/// </para>
/// <para>
/// <b>Karar:</b> yalnızca <see cref="FailureKind.Transient"/> tekrar deneniyor,
/// toplam <see cref="MaxAttempts"/> kez. Zehirli ya da reddedilmiş bir mesajı
/// yeniden denemek, sonucu değişmeyecek bir işi üç kez yapmak ve arkadaki
/// sağlıklı mesajları üç kez bekletmek demek.
/// </para>
/// <para>
/// <b>Neden hemen değil, <see cref="RetryDelay"/> kadar sonra.</b> Mesajı
/// kuyruğun başına geri koymak (<c>requeue: true</c>) onu milisaniyeler içinde
/// tekrar getirir. Hata geçici bir kesintiyse kesinti o sürede geçmemiştir;
/// üç deneme bir saniyenin altında tükenir ve mesaj, geçebilecek bir hata
/// yüzünden ölü kuyruğa düşer.
/// </para>
/// </remarks>
public static class DeliveryPolicy
{
    /// <summary>Bir mesajın en fazla kaç kez işlenmeye çalışılacağı (ilk deneme dahil).</summary>
    public const int MaxAttempts = 3;

    /// <summary>İki deneme arasındaki bekleme.</summary>
    public static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    /// <param name="kind">Başarısızlığın türü.</param>
    /// <param name="attempt">Başarısız olan denemenin numarası; ilk teslim 1.</param>
    public static DeliveryOutcome OnFailure(FailureKind kind, int attempt)
    {
        if (attempt < 1)
            throw new ArgumentOutOfRangeException(nameof(attempt), attempt, "Deneme numarasi 1'den baslar.");

        if (kind != FailureKind.Transient)
            return DeliveryOutcome.DeadLetter;

        return attempt < MaxAttempts ? DeliveryOutcome.Retry : DeliveryOutcome.DeadLetter;
    }
}
