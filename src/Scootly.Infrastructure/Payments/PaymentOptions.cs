namespace Scootly.Infrastructure.Payments;

/// <summary>Ödeme ayarları (69-70. günler).</summary>
public sealed class PaymentOptions
{
    public const string SectionName = "Payments";

    /// <summary>
    /// Webhook imzasının paylaşılan sırrı. user-secrets'ta; depoda YOK.
    /// Tanımlı değilse webhook ucu her isteği reddeder (kapalı başarısızlık).
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>
    /// İmzadaki zaman damgası bundan eskiyse istek reddedilir (tekrar oynatma koruması).
    /// </summary>
    public int WebhookToleranceSeconds { get; set; } = 300;

    /// <summary>
    /// Sahte sağlayıcı bu tutarın ÜSTÜNDEKİ ödemeleri reddeder. Telafi yolunu
    /// denemek için 0 yapılır (her ödeme reddedilir). 71. günde gerçek
    /// simülatör gelince kalkacak.
    /// </summary>
    public decimal FakeDeclineAbove { get; set; } = 1000m;
}
