namespace Scootly.Infrastructure.Messaging;

/// <summary>RabbitMQ bağlantı ayarları (61. gün).</summary>
/// <remarks>
/// Parola yapılandırma dosyasına YAZILMAZ; user-secrets'tan (geliştirme) ya da
/// ortam değişkeninden (üretim) gelir. Varsayılanı da yok: RabbitMQ'nun
/// <c>guest/guest</c> hesabı yalnızca aynı makineden bağlanmaya izin veriyor,
/// yani varsayılan olarak kullanılsaydı Parallels'taki Windows'tan Mac'teki
/// kuyruğa bağlanırken "erişim reddedildi" alırdık ve hatanın sebebi parola
/// değil ağ gibi görünürdü.
/// </remarks>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = "scootly";

    public string Password { get; set; } = string.Empty;

    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Bağlantı zaman aşımı. Kütüphanenin varsayılanı 30 saniye; RabbitMQ
    /// kapalıyken sürüş bitirme isteği yarım dakika asılı kalırdı.
    /// </summary>
    public int ConnectTimeoutSeconds { get; set; } = 3;

    /// <summary>Bir tüketicinin onaylamadan aynı anda elinde tutabileceği mesaj sayısı.</summary>
    public ushort PrefetchCount { get; set; } = 10;

    /// <summary>Yönetim arayüzünde bağlantının adı (<c>scootly-api</c>, <c>scootly-worker</c>).</summary>
    public string ClientName { get; set; } = "scootly";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(HostName))
            throw new InvalidOperationException("RabbitMq:HostName tanimli degil.");

        if (string.IsNullOrWhiteSpace(Password))
            throw new InvalidOperationException(
                "RabbitMq:Password tanimli degil. Gelistirmede: " +
                "dotnet user-secrets set \"RabbitMq:Password\" \"<deger>\" --project <Api ya da Worker projesi>");
    }
}
