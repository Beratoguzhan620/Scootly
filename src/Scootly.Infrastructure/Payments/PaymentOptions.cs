using System.ComponentModel.DataAnnotations;

namespace Scootly.Infrastructure.Payments;

public sealed class PaymentGatewayOptions
{
    public const string SectionName = "PaymentGateway";

    [Required, Url]
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>Tek bir denemenin zaman aşımı.</summary>
    [Range(1, 60)]
    public int AttemptTimeoutSeconds { get; init; } = 5;

    /// <summary>Tüm yeniden denemeler dahil toplam süre sınırı.</summary>
    [Range(1, 300)]
    public int TotalTimeoutSeconds { get; init; } = 20;
}

public sealed class PaymentWebhookOptions
{
    public const string SectionName = "PaymentWebhook";

    /// <summary>Ödeme sağlayıcısıyla paylaşılan HMAC anahtarı (kaynak kodda tutulmaz).</summary>
    [Required, MinLength(32)]
    public string Secret { get; init; } = string.Empty;

    /// <summary>İmzadaki zaman damgasının kabul edilebilir en büyük sapması (tekrar oynatma saldırılarına karşı).</summary>
    [Range(30, 3600)]
    public int ToleranceSeconds { get; init; } = 300;
}
