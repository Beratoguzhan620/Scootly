using Scootly.Domain.Common;

namespace Scootly.Domain.Geo;

/// <summary>
/// Bir merkez noktanın çevresindeki yarıçapı kapsayan dikdörtgen sınır.
/// </summary>
/// <remarks>
/// <para>
/// 43. günün asıl konusu bu tip. "Şu noktanın 500 metre çevresindeki araçlar"
/// sorusunun doğru cevabı Haversine mesafesidir — ama Haversine bir SQL
/// sorgusuna çevrilemez. EF Core çeviremediği bir ifadeyle karşılaştığında
/// (eski sürümlerde) veriyi belleğe çekip filtrelemeyi C# tarafında yapardı:
/// istemci tarafı değerlendirme. Tablo büyüdükçe bu, "tüm araçları ağdan
/// geçirip 3 tanesini göstermek" demek.
/// </para>
/// <para>
/// Çözüm, filtreyi iki adıma bölmek: önce veritabanında <b>kaba ama
/// çevrilebilir</b> bir filtre (bu dikdörtgen — yalnızca iki BETWEEN koşulu,
/// ve <c>ix_vehicles_konum</c> indeksini kullanabiliyor), sonra gerekirse
/// bellekte <b>hassas</b> filtre (Haversine) yalnızca dönen küçük küme üzerinde.
/// </para>
/// <para>
/// Dikdörtgen daireden büyüktür: köşelerde yarıçapın ~1,41 katına kadar uzanır.
/// Yani birkaç fazla kayıt döner — bu, eksik kayıt dönmesinden çok daha iyi bir
/// hata türü. Gerçek çözüm PostGIS ve coğrafi indeks olurdu; bu projede
/// PostGIS kurulmadı ve kurulmasının maliyeti ADR 0015'te yazılı.
/// </para>
/// </remarks>
public sealed class BoundingBox : ValueObject
{
    /// <summary>Ekvatorda bir enlem derecesinin yaklaşık metre karşılığı.</summary>
    private const double MetrePerLatitudeDegree = 111_320d;

    public double MinLatitude { get; }
    public double MaxLatitude { get; }
    public double MinLongitude { get; }
    public double MaxLongitude { get; }

    private BoundingBox(double minLatitude, double maxLatitude, double minLongitude, double maxLongitude)
    {
        MinLatitude = minLatitude;
        MaxLatitude = maxLatitude;
        MinLongitude = minLongitude;
        MaxLongitude = maxLongitude;
    }

    /// <summary>Merkez ve metre cinsinden yarıçaptan sınır kutusu üretir.</summary>
    public static BoundingBox Around(GeoPoint center, double radiusMeters)
    {
        ArgumentNullException.ThrowIfNull(center);

        if (radiusMeters <= 0)
        {
            throw new DomainException("Yarıçap sıfırdan büyük olmalı.");
        }

        var deltaLatitude = radiusMeters / MetrePerLatitudeDegree;

        // Boylam dereceleri kutuplara yaklaştıkça daralır: 60. enlemde bir
        // boylam derecesi ekvatordakinin yarısı kadar mesafedir. Bu düzeltme
        // yapılmazsa kuzeyde kutu gereğinden dar çıkar ve ARAÇ KAYBEDİLİR —
        // yani hata, sessizce eksik sonuç döndürmek olurdu.
        var cosine = Math.Cos(DegreesToRadians(center.Latitude));

        // Kutupta kosinüs sıfıra gider ve bölme patlar. Orada tüm boylamlar
        // birbirine yakın olduğu için kutuyu tam tura açmak doğru davranış.
        var deltaLongitude = Math.Abs(cosine) < 1e-9
            ? 180d
            : radiusMeters / (MetrePerLatitudeDegree * cosine);

        return new BoundingBox(
            Math.Max(center.Latitude - deltaLatitude, -90d),
            Math.Min(center.Latitude + deltaLatitude, 90d),
            Math.Max(center.Longitude - Math.Abs(deltaLongitude), -180d),
            Math.Min(center.Longitude + Math.Abs(deltaLongitude), 180d));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MinLatitude;
        yield return MaxLatitude;
        yield return MinLongitude;
        yield return MaxLongitude;
    }
}
