namespace Scootly.Api.Demo;

/// <summary>
/// Herkese açık demo: Samsun'da bir hizmet bölgesi, araçlar ve kendi kendine sürüş yapan sürücüler. Başka her
/// ortamda kapalıdır.
/// </summary>
public sealed class DemoOptions
{
    public const string SectionName = "Demo";

    public bool Enabled { get; set; }

    /// <summary>Api'nin kendisine yaptığı çağrılar için dinlediği adres.</summary>
    public string BaseUrl { get; set; } = "http://127.0.0.1:8080";

    /// <summary>Sürücülerin ne sıklıkla sürüşe başlayıp bitirdiği.</summary>
    public TimeSpan Tick { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Bir demo sürüşünün yaklaşık süresi.</summary>
    public TimeSpan RideLength { get; set; } = TimeSpan.FromMinutes(4);

    /// <summary>Herkese açık, bu yüzden hiçbir şeyi korumayan ortak sürücü parolası.</summary>
    public string RiderPassword { get; set; } = "Surucu-Demo-2026";
}
