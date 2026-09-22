namespace Scootly.DeviceSimulator;

/// <summary>
/// Tek bir sahte scooter: hareket eder, bataryası azalır (53. gün).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Random.Shared"/> kullanılıyor, örnek başına bir <c>Random</c>
/// değil. Sebep somut: <c>new Random()</c> ile art arda oluşturulan nesneler
/// eski .NET sürümlerinde aynı tohumu alıp AYNI diziyi üretirdi. 200 aracın
/// hepsinin aynı yöne, aynı hızda gitmesi hatası tam olarak böyle oluşur ve
/// bakınca "çalışıyor" görünür. <c>Random.Shared</c> hem iş parçacığı güvenli
/// hem de bu tuzaktan bağışık.
/// </para>
/// <para>
/// Bu sınıf iş parçacığı güvenli DEĞİL ve olması da gerekmiyor: her araç tek
/// bir döngü tarafından ilerletiliyor. Paylaşılan durum bir üst katmanda
/// (sayaçlarda) ve orası <c>Interlocked</c> kullanıyor (57. gün).
/// </para>
/// </remarks>
internal sealed class SimulatedVehicle
{
    /// <summary>Bir adımda kat edilen en büyük mesafe (yaklaşık 11 metre).</summary>
    private const double AdimBoyu = 0.0001d;

    public SimulatedVehicle(string deviceId, double latitude, double longitude)
    {
        DeviceId = deviceId;
        Latitude = latitude;
        Longitude = longitude;
        BatteryPercentage = Random.Shared.Next(40, 101);
    }

    public string DeviceId { get; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public int BatteryPercentage { get; private set; }

    /// <summary>Konumu küçük ve rastgele bir miktar değiştirir.</summary>
    public void Move()
    {
        Latitude = Kirp(Latitude + ((Random.Shared.NextDouble() - 0.5) * 2 * AdimBoyu), -90d, 90d);
        Longitude = Kirp(Longitude + ((Random.Shared.NextDouble() - 0.5) * 2 * AdimBoyu), -180d, 180d);
    }

    /// <summary>Bataryayı bir miktar düşürür; sıfırın altına inmez.</summary>
    /// <remarks>
    /// Sıfırda kalıp devam etmek gerçekçi değil ama bilinçli: simülatör
    /// çalıştıkça araçların hepsi bir süre sonra %0'a iner ve orada kalır.
    /// Böylece 54. günün batarya eşiği taraması, beklemeden test edilebilir
    /// bir durum buluyor.
    /// </remarks>
    public void DrainBattery()
    {
        if (BatteryPercentage > 0 && Random.Shared.NextDouble() < 0.25d)
        {
            BatteryPercentage--;
        }
    }

    private static double Kirp(double deger, double altSinir, double ustSinir) =>
        Math.Min(Math.Max(deger, altSinir), ustSinir);
}
