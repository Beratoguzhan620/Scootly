using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Scootly.Application.Abstractions;
using Scootly.Application.Common;
using Scootly.Application.Fleet.Queries;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
using Scootly.Domain.Telemetry;
using Scootly.Infrastructure.Persistence;
using Xunit.Abstractions;

namespace Scootly.DbLab;

/// <summary>
/// Gün 42 — N+1 avı. Gün 43 — istemci tarafı değerlendirmenin bedeli.
/// </summary>
[Collection(LabCollection.Adi)]
public sealed class Gun42_43_SorguTests
{
    private const double MerkezEnlem = 36.99d;
    private const double MerkezBoylam = 35.33d;
    private const double YaricapMetre = 500d;

    private readonly LabFixture _lab;
    private readonly ITestOutputHelper _cikti;

    public Gun42_43_SorguTests(LabFixture lab, ITestOutputHelper cikti)
    {
        _lab = lab;
        _cikti = cikti;
    }

    [Fact]
    public async Task Gun42_Harita_ucu_tam_olarak_iki_sorgu_calistirir()
    {
        Assert.True(_lab.Hazir, _lab.AtlamaSebebi);

        var sqlKayitlari = new ConcurrentBag<string>();

        await using var db = Lab.Context(satir => sqlKayitlari.Add(satir));

        var handler = new FindNearbyVehiclesQueryHandler(
            db,
            new EfQueryExecutor(),
            new OnbelleksizHaritaOnbellegi());

        var sonuc = await handler.VeritabanindanAsync(
            new FindNearbyVehiclesQuery(MerkezEnlem, MerkezBoylam, YaricapMetre, 1, 20));

        var sorguSayisi = sqlKayitlari.Count(s => s.Contains("Executed DbCommand", StringComparison.Ordinal));

        _cikti.WriteLine($"Donen kayit  : {sonuc.Items.Count}");
        _cikti.WriteLine($"Toplam       : {sonuc.TotalCount}");
        _cikti.WriteLine($"SQL sorgusu  : {sorguSayisi}");
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("Beklenen TAM OLARAK 2: biri COUNT, digeri sayfanin kendisi.");
        _cikti.WriteLine("Toplam kok (aggregate) yuklenseydi, sahip olunan tipler");
        _cikti.WriteLine("(Model, Battery, Location) icin ek sorgular gorunurdu ve");
        _cikti.WriteLine("sayi satir sayisiyla birlikte buyurdu — N+1'in bu projedeki");
        _cikti.WriteLine("hali. DTO'ya projeksiyon bunu engelliyor.");
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("COUNT'un ayri bir sorgu olmasi bedava degil: tablo buyudukce");
        _cikti.WriteLine("maliyeti sayfanin kendisini gecebilir. O noktada imlec");
        _cikti.WriteLine("(cursor) tabanli sayfalamaya gecmek gerekir — teknik borc.");

        Assert.Equal(2, sorguSayisi);
    }

    [Fact]
    public async Task Gun43_Istemci_tarafi_filtreleme_ile_veritabani_filtresi_karsilastirilir()
    {
        Assert.True(_lab.Hazir, _lab.AtlamaSebebi);

        var merkez = new GeoPoint(MerkezEnlem, MerkezBoylam);

        // Isınma.
        _ = await SunucuTarafiAsync(merkez);

        var (sunucuSure, sunucuSayi, sunucuOkunan) = await OlcAsync(() => SunucuTarafiAsync(merkez));
        var (istemciSure, istemciSayi, istemciOkunan) = await OlcAsync(() => IstemciTarafiAsync(merkez));

        _cikti.WriteLine($"Yaricap: {YaricapMetre:N0} m");
        _cikti.WriteLine(new string('=', 68));
        _cikti.WriteLine("Yontem                    |  Sure (ms) | Okunan satir | Sonuc");
        _cikti.WriteLine(new string('-', 68));
        _cikti.WriteLine($"{"Veritabani filtresi",-25} | {sunucuSure,10:N0} | {sunucuOkunan,12:N0} | {sunucuSayi,5}");
        _cikti.WriteLine($"{"Istemci tarafi filtre",-25} | {istemciSure,10:N0} | {istemciOkunan,12:N0} | {istemciSayi,5}");
        _cikti.WriteLine(new string('=', 68));
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("Iki yontem AYNI sonucu bulmuyor ve bu beklenen: sinir kutusu");
        _cikti.WriteLine("bir dikdortgen, Haversine bir daire. Dikdortgen koselerde");
        _cikti.WriteLine("disari tasiyor, yani birkac fazla kayit donuyor. Fazla kayit,");
        _cikti.WriteLine("EKSIK kayittan cok daha iyi bir hata turu.");
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("Asil fark 'okunan satir' sutununda: istemci tarafi filtre");
        _cikti.WriteLine("butun tabloyu agdan gecirip bellekte eliyor. Bu, veri azken");
        _cikti.WriteLine("hic fark edilmez ve veri buyudukce dogrusal olarak kotulesir.");
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("Tam dogru cozum PostGIS ve cografi indeks olurdu; kurulmadi,");
        _cikti.WriteLine("gerekcesi ADR 0015'te.");

        Assert.True(
            sunucuOkunan < istemciOkunan,
            "Veritabani filtresi, istemci tarafi filtreden daha az satir okumaliydi.");
    }

    /// <summary>DOĞRU: filtre SQL'de, yalnızca gereken satırlar okunuyor.</summary>
    private static async Task<(int Sonuc, int Okunan)> SunucuTarafiAsync(GeoPoint merkez)
    {
        await using var db = Lab.Context();

        var kutu = BoundingBox.Around(merkez, YaricapMetre);

        var minEnlem = kutu.MinLatitude;
        var maxEnlem = kutu.MaxLatitude;
        var minBoylam = kutu.MinLongitude;
        var maxBoylam = kutu.MaxLongitude;

        var kayitlar = await db.Vehicles
            .Where(v => v.Status == VehicleStatus.Available)
            .Where(v => v.Location.Latitude >= minEnlem && v.Location.Latitude <= maxEnlem)
            .Where(v => v.Location.Longitude >= minBoylam && v.Location.Longitude <= maxBoylam)
            .Select(v => new NearbyVehicleDto(
                v.Id, v.Location.Latitude, v.Location.Longitude, v.Battery.Percentage))
            .ToListAsync();

        return (kayitlar.Count, kayitlar.Count);
    }

    /// <summary>
    /// YANLIŞ: tüm satırlar belleğe alınıp C# tarafında eleniyor.
    /// </summary>
    /// <remarks>
    /// Bu kod bilerek yazıldı ve depoda kalıyor — 43. günün ölçümünün
    /// "önce" tarafı. Gerçek kodda böyle bir yol YOK; burada olması, bedelin
    /// somut bir sayı olarak görülebilmesi için.
    /// </remarks>
    private static async Task<(int Sonuc, int Okunan)> IstemciTarafiAsync(GeoPoint merkez)
    {
        await using var db = Lab.Context();

        var hepsi = await db.Vehicles
            .Where(v => v.Status == VehicleStatus.Available)
            .Select(v => new NearbyVehicleDto(
                v.Id, v.Location.Latitude, v.Location.Longitude, v.Battery.Percentage))
            .ToListAsync();

        var yakindakiler = hepsi
            .Where(v => merkez.DistanceTo(new GeoPoint(v.Latitude, v.Longitude)) <= YaricapMetre)
            .ToList();

        return (yakindakiler.Count, hepsi.Count);
    }

    private static async Task<(long Sure, int Sonuc, int Okunan)> OlcAsync(
        Func<Task<(int Sonuc, int Okunan)>> is_)
    {
        var kronometre = Stopwatch.StartNew();
        var (sonuc, okunan) = await is_();
        kronometre.Stop();

        return (kronometre.ElapsedMilliseconds, sonuc, okunan);
    }
}

/// <summary>
/// 44. gün — toplu yazma. <c>COPY</c> ile tek tek <c>INSERT</c> karşılaştırması.
/// </summary>
[Collection(LabCollection.Adi)]
public sealed class Gun44_TopluYazmaTests
{
    private const int KayitSayisi = 5_000;

    private readonly LabFixture _lab;
    private readonly ITestOutputHelper _cikti;

    public Gun44_TopluYazmaTests(LabFixture lab, ITestOutputHelper cikti)
    {
        _lab = lab;
        _cikti = cikti;
    }

    [Fact]
    public async Task Toplu_yazma_tek_tek_INSERT_ile_karsilastirilir()
    {
        Assert.True(_lab.Hazir, _lab.AtlamaSebebi);

        var topluOlcumler = Olcumler(KayitSayisi);
        var tekilOlcumler = Olcumler(KayitSayisi);

        ITelemetryWriter yazici = new TelemetryBulkWriter(
            new TelemetryWriteOptions(Lab.LabBaglantisi!));

        var topluKronometre = Stopwatch.StartNew();
        var yazilan = await yazici.WriteBatchAsync(topluOlcumler);
        topluKronometre.Stop();

        var tekilKronometre = Stopwatch.StartNew();
        await using (var db = Lab.Context())
        {
            // EF ile tek tek: her kayit once degisiklik izleyicisine giriyor,
            // sonra SaveChanges toplu INSERT ifadeleri uretiyor. COPY'den
            // farki, SQL ayristirma ve planlama adiminin her grup icin
            // yeniden calismasi.
            await db.TelemetryReadings.AddRangeAsync(tekilOlcumler);
            await db.SaveChangesAsync();
        }
        tekilKronometre.Stop();

        _cikti.WriteLine($"Kayit sayisi : {KayitSayisi:N0}");
        _cikti.WriteLine(new string('=', 54));
        _cikti.WriteLine("Yontem                     |     Sure (ms)");
        _cikti.WriteLine(new string('-', 54));
        _cikti.WriteLine($"{"COPY (TelemetryBulkWriter)",-26} | {topluKronometre.ElapsedMilliseconds,13:N0}");
        _cikti.WriteLine($"{"EF AddRange + SaveChanges",-26} | {tekilKronometre.ElapsedMilliseconds,13:N0}");
        _cikti.WriteLine(new string('=', 54));
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("COPY'nin kazandigi sey SQL ayristirma ve planlama adimi:");
        _cikti.WriteLine("satirlar ikili (binary) bicimde tek akista gidiyor, hicbiri");
        _cikti.WriteLine("ayri bir ifade olarak yorumlanmiyor.");
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("Odenen bedel: TelemetryBulkWriter tablo ve sutun adlarini");
        _cikti.WriteLine("METIN olarak biliyor. Sema degisirse derleme degil, CALISMA");
        _cikti.WriteLine("ZAMANI hatasi verir. Bu test o bedeli de kapsiyor: kolon");
        _cikti.WriteLine("adlari yanlis olsaydi burasi kirmizi olurdu.");

        Assert.Equal(KayitSayisi, yazilan);
    }

    private static List<TelemetryReading> Olcumler(int adet) =>
        Enumerable.Range(0, adet)
            .Select(i => new TelemetryReading(
                Guid.NewGuid(),
                new DeviceId("lab-" + (i % 200).ToString("D4", CultureInfo.InvariantCulture)),
                new GeoPoint(36.99 + (i % 100 * 0.0001), 35.33 + (i % 100 * 0.0001)),
                i % 101,
                DateTime.UtcNow))
            .ToList();
}
