using System.Globalization;
using Scootly.Application.Fleet.Queries;
using Scootly.Application.Pricing.Queries;

namespace Scootly.Infrastructure.Caching;

/// <summary>
/// Bütün önbellek anahtarlarının üretildiği tek yer (46. gün).
/// </summary>
/// <remarks>
/// <para>
/// Anahtarların tek bir yerde olmasının sebebi geçersizleştirme. Anahtar
/// metinleri çağrı yerlerine dağılsaydı, yazan taraf ile silen tarafın aynı
/// metni ürettiğinden emin olmanın bir yolu kalmazdı — ve ayrıştıkları gün
/// önbellek hiç temizlenmez, kullanıcı süresiz bayat veri görürdü. 49. günün
/// "en sinsi hata" dediği durum tam olarak böyle oluşuyor.
/// </para>
/// <para>
/// Aktif tarife anahtarı burada yeniden YAZILMIYOR, handler'ın sabitinden
/// okunuyor. İki yerde yazılmış aynı metin, er geç iki farklı metindir.
/// </para>
/// <para>
/// <c>scootly:</c> öneki bu sınıfta DEĞİL, <see cref="RedisCacheService"/>
/// içinde ekleniyor — ad alanı (namespace) bir depolama detayı, anahtarın
/// anlamı değil. Böylece bellek içi uygulama önek taşımıyor ve iki uygulama
/// arasında geçiş anahtarları değiştirmiyor.
/// </para>
/// </remarks>
public static class CacheKeys
{
    /// <summary>Harita sorgusu önbelleğinin ortak öneki (toplu silme için).</summary>
    public const string YakinAraclarOneki = "yakin:";

    /// <summary>Aktif tarife. Tek kaynak: handler'ın kendi sabiti.</summary>
    public static string AktifTarife() => GetActiveTariffQueryHandler.OnbellekAnahtari;

    /// <summary>
    /// Belirli bir harita sorgusunun anahtarı.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Koordinatlar <b>yuvarlanıyor</b>. Ham koordinat kullanılsaydı her
    /// kullanıcının birkaç metrelik farkı ayrı bir anahtar üretir, isabet oranı
    /// sıfıra yaklaşır ve önbellek yalnızca bellek harcayan bir katmana
    /// dönüşürdü. Üç ondalık basamak ~110 metrelik bir ızgara demek: aynı
    /// sokaktaki iki kullanıcı aynı anahtarı paylaşıyor.
    /// </para>
    /// <para>
    /// <see cref="CultureInfo.InvariantCulture"/> zorunlu. Türkçe yerelinde
    /// ondalık ayırıcı virgüldür; biçimlendirme kültüre bırakılsaydı aynı
    /// koordinat makineden makineye farklı anahtar üretebilir ve iki API
    /// kopyası birbirinin önbelleğini hiç göremezdi — 47. günde kurulan
    /// paylaşımlı önbelleğin sessizce işe yaramaz hale geldiği yer burası
    /// olurdu.
    /// </para>
    /// </remarks>
    public static string YakinAraclar(FindNearbyVehiclesQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var enlem = query.Latitude.ToString("F3", CultureInfo.InvariantCulture);
        var boylam = query.Longitude.ToString("F3", CultureInfo.InvariantCulture);
        var yaricap = query.RadiusMeters.ToString("F0", CultureInfo.InvariantCulture);
        var sayfa = query.PageNumber.ToString(CultureInfo.InvariantCulture);
        var boyut = query.PageSize.ToString(CultureInfo.InvariantCulture);

        return $"{YakinAraclarOneki}{enlem}:{boylam}:{yaricap}:{sayfa}:{boyut}";
    }

    /// <summary>Bir aracın rezervasyonu için dağıtık kilit anahtarı (50. gün).</summary>
    public static string AracKilidi(Guid vehicleId) => "kilit:arac:" + vehicleId.ToString("N");
}
