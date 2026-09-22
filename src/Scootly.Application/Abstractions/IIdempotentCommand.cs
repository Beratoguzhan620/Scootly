namespace Scootly.Application.Abstractions;

/// <summary>
/// Aynı isteğin iki kez gelmesinin ikinci kez etki etmemesi gerektiğini
/// bildirir (60. gün deneyi — ARAYÜZ yaklaşımı).
/// </summary>
/// <remarks>
/// <para>
/// Tekrara dayanıklılık (idempotency) bu sistemde teorik bir ihtiyaç değil:
/// 53. günün cihaz simülatörü ağ hatasında yeniden deniyor, mobil istemci de
/// deneyecek. "Rezervasyon oluştur" isteği iki kez ulaşırsa ikinci istek yeni
/// bir rezervasyon üretmemeli.
/// </para>
/// <para>
/// Komutun kendisi anahtarı taşıyor. Anahtarı ÜRETEN taraf istemci: sunucu
/// üretseydi, iki isteğin aynı istek olduğunu anlamanın bir yolu kalmazdı —
/// zaten anlaşılması gereken şey tam olarak bu.
/// </para>
/// </remarks>
public interface IIdempotentCommand
{
    /// <summary>
    /// İstemcinin ürettiği, bu mantıksal işlemi tekil olarak tanımlayan anahtar.
    /// </summary>
    string IdempotencyKey { get; }
}

/// <summary>
/// Bir idempotency anahtarının daha önce işlenip işlenmediğini tutar.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bellek içi bir uygulama YANLIŞ olurdu.</b> İki API kopyası çalıştığında
/// her biri kendi kaydını tutar; aynı isteğin tekrarı diğer kopyaya düşerse
/// yeni sayılır. 46. günde bellek içi önbellek için öğrenilen dersin aynısı.
/// Gerçek uygulama 47. günde kurulan Redis üzerinde olmalı.
/// </para>
/// <para>
/// Yaşam süresi zorunlu: istemcinin yeniden deneme penceresinden uzun,
/// sonsuzdan kısa. Sonsuz saklamak, anahtar tablosunun sınırsız büyümesi demek.
/// </para>
/// </remarks>
public interface IIdempotencyStore
{
    /// <summary>
    /// Anahtarı ilk kez görüyorsak kaydeder ve <c>true</c> döner.
    /// Daha önce görülmüşse hiçbir şey yapmaz ve <c>false</c> döner.
    /// </summary>
    /// <remarks>
    /// Tek metot olması bilinçli. "Var mı" ve "kaydet" iki ayrı çağrı olsaydı
    /// arasında ikinci bir istek geçebilirdi — 36. günde ölçtüğümüz
    /// oku-kontrol-yaz yarışının aynısı. Redis'te bu <c>SET NX</c> ile tek
    /// komutta yapılıyor.
    /// </remarks>
    Task<bool> IlkKezMiAsync(string key, TimeSpan timeToLive, CancellationToken cancellationToken = default);
}
