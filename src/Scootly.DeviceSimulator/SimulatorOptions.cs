namespace Scootly.DeviceSimulator;

/// <summary>
/// Yapılandırma kaynakları (öncelik sırasıyla): komut satırı, SCOOTLY_ önekli ortam değişkenleri,
/// user-secrets, appsettings.json. Cihaz sırrı kaynak kodda tutulmaz.
/// </summary>
public sealed class SimulatorOptions
{
    public string ApiBaseUrl { get; init; } = "http://localhost:5016";

    public string ClientId { get; init; } = "scootly-device-simulator";

    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>Simüle edilecek en fazla araç sayısı (API'de kayıtlı araçlardan seçilir).</summary>
    public int MaxVehicles { get; init; } = 200;

    public int IntervalSeconds { get; init; } = 5;
}
