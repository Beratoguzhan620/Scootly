using System.Data;
using Xunit.Abstractions;

namespace Scootly.DbLab;

/// <summary>
/// Gün 36 — yarış durumunu gözle görmek.
/// Gün 37 — izolasyon seviyelerinin bu soruna ne yaptığını ölçmek.
/// </summary>
[Collection(LabCollection.Adi)]
public sealed class Gun36_37_YarisVeIzolasyonTests
{
    private const int EszamanliIstek = 50;

    private readonly LabFixture _lab;
    private readonly ITestOutputHelper _cikti;

    public Gun36_37_YarisVeIzolasyonTests(LabFixture lab, ITestOutputHelper cikti)
    {
        _lab = lab;
        _cikti = cikti;
    }

    [Fact]
    public async Task Gun36_Korumasiz_kodda_ayni_arac_birden_fazla_kisiye_rezerve_edilir()
    {
        Assert.True(_lab.Hazir, _lab.AtlamaSebebi);

        var aracId = await LabVeri.YeniMusaitAracAsync();

        var denemeler = Enumerable.Range(0, EszamanliIstek)
            .Select(_ => KorumasizRezervasyon.DeneAsync(aracId, IsolationLevel.ReadCommitted));

        var sonuclar = await Task.WhenAll(denemeler);
        var kazanan = sonuclar.Count(b => b);

        _cikti.WriteLine($"{EszamanliIstek} esz aman li istek -> {kazanan} tanesi BASARILI oldu");
        _cikti.WriteLine($"Aracin son durumu: {await LabVeri.DurumAsync(aracId)}");
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("Olan su: her istek 'arac musait mi' diye sordu, hepsi 'evet'");
        _cikti.WriteLine("cevabini aldi (cunku hicbiri digerinin henuz yazmadigi veriyi");
        _cikti.WriteLine("okuyordu) ve hepsi rezervasyonu tamamladi. Tek bir arac,");
        _cikti.WriteLine($"{kazanan} kisiye ayni anda rezerve edildi.");
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("Tek is parcacikli bir testle bu hata ASLA gorulemez.");

        // Bu testin iddiasi "kod bozuk" degil, "bu YAZIM BICIMI bozuk".
        // Birden fazla kazanan cikmasi, korumanin gerekli oldugunun kaniti.
        Assert.True(
            kazanan > 1,
            $"Yaris durumu uretilemedi (kazanan: {kazanan}). Gecikme penceresi yetersiz olabilir.");
    }

    [Fact]
    public async Task Gun37_Izolasyon_seviyeleri_karsilastirilir()
    {
        Assert.True(_lab.Hazir, _lab.AtlamaSebebi);

        var seviyeler = new[]
        {
            IsolationLevel.ReadCommitted,
            IsolationLevel.RepeatableRead,
            IsolationLevel.Serializable
        };

        _cikti.WriteLine($"Her seviyede {EszamanliIstek} esz aman li rezervasyon denemesi");
        _cikti.WriteLine(new string('=', 60));
        _cikti.WriteLine("Seviye              | Kazanan | Son durum");
        _cikti.WriteLine(new string('-', 60));

        var sonuclar = new Dictionary<IsolationLevel, int>();

        foreach (var seviye in seviyeler)
        {
            var aracId = await LabVeri.YeniMusaitAracAsync();

            var denemeler = Enumerable.Range(0, EszamanliIstek)
                .Select(_ => KorumasizRezervasyon.DeneAsync(aracId, seviye));

            var cevaplar = await Task.WhenAll(denemeler);
            var kazanan = cevaplar.Count(b => b);
            sonuclar[seviye] = kazanan;

            _cikti.WriteLine($"{seviye,-19} | {kazanan,7} | {await LabVeri.DurumAsync(aracId)}");
        }

        _cikti.WriteLine(new string('=', 60));
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("Okuma notlari:");
        _cikti.WriteLine(" - ReadCommitted: yalnizca commit edilmis veriyi gorursun, ama");
        _cikti.WriteLine("   ayni islem icinde tekrar okudugunda veri degismis olabilir.");
        _cikti.WriteLine("   Cift rezervasyonu ENGELLEMEZ.");
        _cikti.WriteLine(" - RepeatableRead: okudugun satirlarin degismeyecegini garanti");
        _cikti.WriteLine("   eder. PostgreSQL bunu anlik goruntu (snapshot) ile saglar;");
        _cikti.WriteLine("   catisan yazmalarda islemi iptal eder (40001).");
        _cikti.WriteLine(" - Serializable: islemler sanki sirayla calismis gibi davranir.");
        _cikti.WriteLine("   En guclu garanti, en cok iptal, en dusuk paralellik.");
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("Bedeli soyle okunur: iptal edilen her islem, kullaniciya bir");
        _cikti.WriteLine("hata ya da bir yeniden deneme demek. 'En guvenlisini secelim'");
        _cikti.WriteLine("demeden once bu bedeli olcmek gerekiyordu — bu tablo o olcum.");

        // ReadCommitted'in korumadigi, digerlerinin ondan daha iyi oldugu
        // beklenen sonuc. Kesin sayi dayatilmiyor: zamanlamaya bagli.
        Assert.True(sonuclar[IsolationLevel.ReadCommitted] > 1);
        Assert.True(
            sonuclar[IsolationLevel.Serializable] <= sonuclar[IsolationLevel.ReadCommitted],
            "Serializable, ReadCommitted'dan daha fazla kazanan uretmemeli.");
    }
}
