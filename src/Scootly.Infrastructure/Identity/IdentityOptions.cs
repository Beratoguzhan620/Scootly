using System.ComponentModel.DataAnnotations;

namespace Scootly.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 imza anahtarı. Kaynak kodda veya appsettings'te tutulmaz (user-secrets / ortam değişkeni).</summary>
    [Required, MinLength(32)]
    public string Key { get; init; } = string.Empty;

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Range(1, 1440)]
    public int ExpiryMinutes { get; init; } = 60;

    /// <summary>
    /// Token'daki güvenlik damgasının veritabanıyla karşılaştırma sonucunun süreç içinde tutulma süresi.
    /// Rol değişikliği veya hesap silme, diğer kopyalarda en geç bu süre sonunda etkili olur (0: her istekte sorgu).
    /// </summary>
    [Range(0, 600)]
    public int SecurityStampCacheSeconds { get; init; } = 30;
}

/// <summary>
/// SignalR bağlantısı için kısa ömürlü token. Ana JWT'den ayrı bir anahtarla imzalanır ve ayrı bir hedef kitleye
/// verilir; Api bu token'ı yalnızca hub uçlarında kabul eder. Mvc yalnızca bu anahtarı bilir, ana API token'ı üretemez.
/// </summary>
public sealed class HubTokenOptions
{
    public const string SectionName = "Jwt";
    public const string DefaultAudience = "Scootly.Hub";

    [Required, MinLength(32)]
    public string HubKey { get; init; } = string.Empty;

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string HubAudience { get; init; } = DefaultAudience;

    [Range(1, 120)]
    public int HubTokenMinutes { get; init; } = 15;
}

/// <summary>
/// Cihaz ağ geçidi (gateway) kimlik bilgileri: telemetri gönderen tek bir istemci birden fazla aracı temsil eder,
/// her okuma kayıtlı bir araca ait olmalıdır (bkz. ADR 0021).
/// </summary>
public sealed class DeviceAuthOptions
{
    public const string SectionName = "DeviceAuth";

    [Required]
    public string ClientId { get; init; } = string.Empty;

    [Required, MinLength(32)]
    public string ClientSecret { get; init; } = string.Empty;

    [Range(5, 1440)]
    public int TokenLifetimeMinutes { get; init; } = 60;
}

/// <summary>
/// İlk filo yöneticisi hesabı. Yalnızca ayarlanmışsa ve bu e-postayla bir hesap henüz yoksa açılışta oluşturulur;
/// var olan bir hesaba asla yetki verilmez.
/// </summary>
public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    public string? FleetManagerEmail { get; init; }

    public string? FleetManagerPassword { get; init; }
}
