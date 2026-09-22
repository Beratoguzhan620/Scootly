using System.Globalization;
using System.Text;
using Xunit.Abstractions;

namespace Scootly.DbLab;

/// <summary>
/// Gün 59 — sıcak yoldaki gereksiz tahsisler ve çöp toplayıcı.
/// </summary>
/// <remarks>
/// <para>
/// Çöp toplama (GC) otomatik ama bedava değil. Nesil bazlı çalışıyor: kısa
/// ömürlü nesneler ucuz toplanıyor, ama <b>çok sayıda</b> kısa ömürlü nesne,
/// toplayıcıyı sık çalıştırıyor ve her çalışma bir duraklama demek. Yük
/// altında bu, p95 yanıt süresinde sıçrama olarak görünüyor — ortalama iyi
/// kalırken bazı isteklerin fark edilir biçimde yavaşlaması.
/// </para>
/// <para>
/// Ölçülen şey süre değil <b>tahsis edilen bayt</b>.
/// <c>GC.GetAllocatedBytesForCurrentThread</c> bunu doğrudan veriyor ve
/// süreden çok daha kararlı bir ölçü: makinenin o anki yüküne göre değişmiyor.
/// </para>
/// </remarks>
public sealed class Gun59_BellekTests
{
    private const int KayitSayisi = 10_000;

    private readonly ITestOutputHelper _cikti;

    public Gun59_BellekTests(ITestOutputHelper cikti)
    {
        _cikti = cikti;
    }

    [Fact]
    public void Dongu_icinde_metin_birlestirme_olculur()
    {
        var kayitlar = Enumerable.Range(0, KayitSayisi)
            .Select(i => (Cihaz: "sim-" + i.ToString("D4", CultureInfo.InvariantCulture), Yuzde: i % 101))
            .ToList();

        // Isınma: ilk çalıştırmanın JIT ve statik kurucu maliyetleri ölçüme
        // karışmasın.
        _ = KotuBirlestirme(kayitlar.Take(10).ToList());
        _ = IyiBirlestirme(kayitlar.Take(10).ToList());

        var kotu = TahsisOlc(() => KotuBirlestirme(kayitlar));
        var iyi = TahsisOlc(() => IyiBirlestirme(kayitlar));

        _cikti.WriteLine($"Kayit sayisi : {KayitSayisi:N0}");
        _cikti.WriteLine(new string('=', 56));
        _cikti.WriteLine("Yontem                        |     Tahsis (KB)");
        _cikti.WriteLine(new string('-', 56));
        _cikti.WriteLine($"{"Dongu icinde string +=",-29} | {kotu / 1024d,15:N0}");
        _cikti.WriteLine($"{"StringBuilder (on boyutlu)",-29} | {iyi / 1024d,15:N0}");
        _cikti.WriteLine(new string('=', 56));
        _cikti.WriteLine($"Oran: {(double)kotu / iyi:0.0}x");
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("Sebep: string degismez (immutable). Dongu icinde += yazmak,");
        _cikti.WriteLine("her adimda ESKI metnin tamamini yeni bir diziye kopyalamak");
        _cikti.WriteLine("demek. N adimda tahsis N'in KARESIYLE buyuyor.");
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("Bu hata neden sinsi: kucuk veriyle fark edilmiyor. On kayitla");
        _cikti.WriteLine("iki yontem arasinda olculebilir fark yok; on bin kayitla");
        _cikti.WriteLine("aradaki fark yuzlerce kat.");

        Assert.True(
            kotu > iyi,
            $"Dongu icinde birlestirme ({kotu:N0} bayt) StringBuilder'dan " +
            $"({iyi:N0} bayt) daha az tahsis etti — olcum hatali olmali.");
    }

    [Fact]
    public void Nesil_bazli_toplama_gozlemlenir()
    {
        var oncekiGen0 = GC.CollectionCount(0);
        var oncekiGen2 = GC.CollectionCount(2);

        // Bol miktarda kısa ömürlü nesne: hepsi Gen0'da doğuyor ve orada
        // ölüyor. Gen2'ye hiç terfi etmemeleri beklenen davranış.
        for (var i = 0; i < 200_000; i++)
        {
            var gecici = new byte[128];
            GC.KeepAlive(gecici);
        }

        var gen0 = GC.CollectionCount(0) - oncekiGen0;
        var gen2 = GC.CollectionCount(2) - oncekiGen2;

        _cikti.WriteLine($"Gen0 toplamasi : {gen0}");
        _cikti.WriteLine($"Gen2 toplamasi : {gen2}");
        _cikti.WriteLine($"Sunucu GC modu : {System.Runtime.GCSettings.IsServerGC}");
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("Kisa omurlu nesneler Gen0'da dogup orada oluyor; Gen0");
        _cikti.WriteLine("toplamasi ucuz. Pahali olan Gen2 — ve oraya terfi eden sey,");
        _cikti.WriteLine("beklenenden uzun yasayan nesneler oluyor (ornegin sinirsiz");
        _cikti.WriteLine("buyuyen bir bellek ici onbellek; 46. gunde SizeLimit'in");
        _cikti.WriteLine("konulma sebebi tam olarak bu).");

        Assert.True(gen0 >= 0);
    }

    /// <summary>YANLIŞ: her adımda tüm metin yeniden kopyalanıyor.</summary>
    private static string KotuBirlestirme(List<(string Cihaz, int Yuzde)> kayitlar)
    {
        var sonuc = string.Empty;

        foreach (var kayit in kayitlar)
        {
            sonuc += kayit.Cihaz + "=" + kayit.Yuzde.ToString(CultureInfo.InvariantCulture) + ";";
        }

        return sonuc;
    }

    /// <summary>DOĞRU: tek bir arabellek, önceden yaklaşık boyutlu.</summary>
    private static string IyiBirlestirme(List<(string Cihaz, int Yuzde)> kayitlar)
    {
        // Kapasiteyi önceden vermek, arabelleğin büyürken tekrar tekrar
        // kopyalanmasını da engelliyor.
        var arabellek = new StringBuilder(kayitlar.Count * 16);

        foreach (var kayit in kayitlar)
        {
            arabellek.Append(kayit.Cihaz).Append('=').Append(kayit.Yuzde).Append(';');
        }

        return arabellek.ToString();
    }

    private static long TahsisOlc(Action is_)
    {
        // Ölçümden önce toplama: önceki testlerin çöpü bu ölçüme karışmasın.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var once = GC.GetAllocatedBytesForCurrentThread();
        is_();
        return GC.GetAllocatedBytesForCurrentThread() - once;
    }
}
