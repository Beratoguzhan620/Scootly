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

/// <summary>İlk filo yöneticisi hesabı. Yalnızca ayarlanmışsa uygulama açılışında oluşturulur/güncellenir.</summary>
public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    public string? FleetManagerEmail { get; init; }

    public string? FleetManagerPassword { get; init; }
}
