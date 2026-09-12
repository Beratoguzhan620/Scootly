namespace Scootly.Infrastructure.Devices;

/// <summary>
/// Bir araç cihazının kimlik bilgisi.
/// </summary>
/// <remarks>
/// Sır DÜZ METİN olarak saklanmaz; kullanıcı parolalarında olduğu gibi hash'lenir.
/// Cihaz sırrı bir paroladan farklı değildir: veritabanı sızarsa, düz metin
/// saklanmış bir sır saldırganın o cihaz adına token almasına yeter.
/// </remarks>
public sealed class DeviceCredential
{
    private DeviceCredential()
    {
        DeviceId = string.Empty;
        SecretHash = string.Empty;
    }

    public DeviceCredential(string deviceId, string secretHash)
    {
        DeviceId = deviceId;
        SecretHash = secretHash;
        IsActive = true;
    }

    /// <summary>Cihazın genel kimliği (gizli değil).</summary>
    public string DeviceId { get; private set; }

    /// <summary>Cihaz sırrının hash'i. Sırrın kendisi hiçbir yerde saklanmaz.</summary>
    public string SecretHash { get; private set; }

    /// <summary>
    /// Kapatılan cihaz token alamaz. Kaydı silmek yerine pasifleştirmek,
    /// bir cihazın ne zaman ve neden devre dışı bırakıldığının kaydını korur.
    /// </summary>
    public bool IsActive { get; private set; }

    public void Deactivate() => IsActive = false;
}
