namespace Scootly.Testing;

/// <summary>
/// Yalnızca geçici test sunucularında kullanılan, her test çalıştırmasında yeniden üretilen değerler.
/// Geliştiricinin user-secrets'ına veya herhangi bir gerçek sırra bağımlılık yoktur.
/// </summary>
public static class TestSecrets
{
    public static readonly string JwtKey = RandomSecret();
    public static readonly string DeviceClientSecret = RandomSecret();
    public static readonly string WebhookSecret = RandomSecret();

    public const string DeviceClientId = "scootly-device-simulator";
    public const string DefaultPassword = "TestParola123";

    private static string RandomSecret() => Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
}
