using System.Text;

namespace Scootly.Infrastructure.Identity;

/// <summary>
/// Token üretimi ve doğrulaması için yapılandırma. Değerler user-secrets'tan
/// (geliştirme) veya ortam değişkenlerinden (üretim) gelir; hiçbiri koda gömülü değildir.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// HMAC-SHA256 imzası için gereken en az anahtar uzunluğu (bayt).
    /// 256 bit = 32 bayt. Daha kısa bir anahtar, imzanın hash fonksiyonunun
    /// sunduğu güvenlik seviyesinin altına düşmesi demektir.
    /// </summary>
    public const int MinimumSigningKeyBytes = 32;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Token'ın geçerlilik süresi. Kısa tutulur: token iptal edilemez, sızan bir
    /// token süresi dolana kadar geçerli kalır. Süreyi kısaltmak bu pencereyi daraltır.
    /// </summary>
    public int AccessTokenLifetimeMinutes { get; set; } = 60;

    /// <summary>
    /// Yapılandırma eksikse veya zayıfsa istisna fırlatır.
    /// Program.cs bunu açılışta çağırır: uygulamanın imzasız veya zayıf imzalı
    /// token üretecek şekilde AYAKTA KALMASINDANSA hiç başlamaması tercih edilir.
    /// Sessizce varsayılana düşen bir imza anahtarı, herkesin kendine yönetici
    /// token'ı üretebilmesi demektir.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer))
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(Issuer)} tanımlı değil.");
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(Audience)} tanımlı değil.");
        }

        if (string.IsNullOrWhiteSpace(SigningKey))
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(SigningKey)} tanımlı değil. " +
                "user-secrets ile ayarla; kaynak koda yazma.");
        }

        var keyBytes = Encoding.UTF8.GetByteCount(SigningKey);
        if (keyBytes < MinimumSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(SigningKey)} çok kısa ({keyBytes} bayt). " +
                $"HMAC-SHA256 için en az {MinimumSigningKeyBytes} bayt gerekir.");
        }

        if (AccessTokenLifetimeMinutes is < 1 or > 1440)
        {
            throw new InvalidOperationException(
                $"{SectionName}:{nameof(AccessTokenLifetimeMinutes)} 1 ile 1440 arasında olmalı.");
        }
    }
}
