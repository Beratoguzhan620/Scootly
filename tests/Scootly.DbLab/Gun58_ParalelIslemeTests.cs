using System.Diagnostics;
using Scootly.Domain.Geo;
using Xunit.Abstractions;

namespace Scootly.DbLab;

/// <summary>
/// Gün 58 — paralel işleme ne zaman işe yarıyor, ne zaman yaramıyor.
/// </summary>
/// <remarks>
/// <para>
/// "Paralel daha hızlıdır" yanlış bir varsayım. Her paralel görev bir ek yük
/// taşıyor: görevi kuyruğa koymak, bir iş parçacığına vermek, bitmesini
/// beklemek, sonuçları toplamak. İş parçası küçükse bu ek yük kazancı yutar.
/// </para>
/// <para>
/// <b>İlk sürüm 10.000 kayıtla çalışıyordu ve sıralı geçiş 1 ms sürüyordu.</b>
/// O çözünürlükte ölçülen şey iş değil gürültüydü: beklenen sonuç ne
/// doğrulanabiliyor ne çürütülebiliyordu. Veri kümesi 500.000'e çıkarıldı ve
/// her ölçüm üç kez tekrarlanıp medyanı alınıyor.
/// </para>
/// <para>
/// Bu da Faz 3'ün kendi dersi: ölçmeden iddia etmek kadar, ölçemeyecek kadar
/// küçük bir deneyle iddia etmek de yanlış.
/// </para>
/// <para>
/// Ölçülen iş gerçek: telemetri kayıtlarının bir önceki konuma göre kat ettiği
/// mesafe (Haversine). CPU'ya bağlı bir hesap — I/O beklemeli bir iş olsaydı
/// <c>Parallel.ForEach</c> zaten yanlış araç olurdu.
/// </para>
/// </remarks>
[Collection(EszamanlilikKoleksiyonu.Adi)]
public sealed class Gun58_ParalelIslemeTests
{
    private const int KayitSayisi = 500_000;

    private readonly ITestOutputHelper _cikti;

    public Gun58_ParalelIslemeTests(ITestOutputHelper cikti)
    {
        _cikti = cikti;
    }

    [Fact]
    public void Paralel_isleme_grup_boyutuna_gore_olculur()
    {
        var noktalar = NoktalarUret(KayitSayisi);

        // Isınma turu: ilk çalıştırma JIT derlemesini de ölçerdi ve o maliyet
        // ölçmek istediğimiz şeye ait değil.
        _ = SiraliTopla(noktalar);
        _ = ParalelTopla(noktalar, 100);

        var siraliSure = Olc(() => SiraliTopla(noktalar));

        _cikti.WriteLine($"Kayit sayisi : {KayitSayisi:N0}");
        _cikti.WriteLine($"Cekirdek     : {Environment.ProcessorCount}");
        _cikti.WriteLine("Her olcum uc kez tekrarlandi, medyan alindi.");
        _cikti.WriteLine(new string('=', 58));
        _cikti.WriteLine("Yontem                     |   Sure (ms) | Sirali'ya gore");
        _cikti.WriteLine(new string('-', 58));
        _cikti.WriteLine($"{"Sirali (foreach)",-26} | {siraliSure,11:0.00} | {"1.00x",14}");

        foreach (var grupBoyutu in new[] { 1, 10, 100, 1_000, 10_000 })
        {
            var sure = Olc(() => ParalelTopla(noktalar, grupBoyutu));
            var oran = siraliSure / sure;

            _cikti.WriteLine(
                $"{$"Paralel ({grupBoyutu} kayitlik)",-26} | {sure,11:0.00} | {oran,13:0.00}x");
        }

        _cikti.WriteLine(new string('=', 58));
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("Okuma notu: grup kucukken paralelligin kazanci dusuyor,");
        _cikti.WriteLine("cunku her grup icin odenen ek yuk (gorev olusturma, is");
        _cikti.WriteLine("parcacigina dagitma, baglam degistirme) grubun kendi isine");
        _cikti.WriteLine("kiyasla buyuyor. Grup buyudukce ek yuk sabit kalirken is");
        _cikti.WriteLine("artiyor ve kazanc cekirdek sayisina yaklasiyor.");
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("YUKARIDAKI TABLO NE DERSE O GECERLI. Bu makinede 1'er");
        _cikti.WriteLine("kayitlik gruplar bile siralidan hizli cikabilir; o zaman");
        _cikti.WriteLine("dogru cumle 'paralellik kaybeder' degil, 'kazanc grup");
        _cikti.WriteLine("boyutuyla birlikte artar' olur. Olcum beklentiyi yenerse");
        _cikti.WriteLine("yazilacak olan olcumdur.");
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("Bu tablodan cikan karar: telemetri isleme hattinda");
        _cikti.WriteLine("Parallel.ForEach kullanmiyoruz. Gercek darbogaz CPU degil,");
        _cikti.WriteLine("veritabanina yazma — ve orada cozum paralellik degil toplu");
        _cikti.WriteLine("yazma (44. gun, COPY).");

        // Sayısal bir eşik dayatılmıyor: sonuç çekirdek sayısına ve makinenin
        // o anki yüküne bağlı. İddia edilen tek şey, ölçümün gerçekten
        // ölçülebilir bir büyüklükte olması.
        Assert.True(
            siraliSure > 5d,
            $"Sirali gecis {siraliSure:0.00} ms surdu — olculemeyecek kadar kisa. " +
            "KayitSayisi artirilmali, yoksa tablo gurultu olcer.");
    }

    private static List<GeoPoint> NoktalarUret(int adet)
    {
        var rastgele = new Random(12345); // Sabit tohum: ölçüm tekrarlanabilir olsun.
        var liste = new List<GeoPoint>(adet);

        for (var i = 0; i < adet; i++)
        {
            liste.Add(new GeoPoint(
                37.0 + ((rastgele.NextDouble() - 0.5) * 0.1),
                35.3 + ((rastgele.NextDouble() - 0.5) * 0.1)));
        }

        return liste;
    }

    private static double SiraliTopla(List<GeoPoint> noktalar)
    {
        var toplam = 0d;

        for (var i = 1; i < noktalar.Count; i++)
        {
            toplam += noktalar[i - 1].DistanceTo(noktalar[i]);
        }

        return toplam;
    }

    private static double ParalelTopla(List<GeoPoint> noktalar, int grupBoyutu)
    {
        var gruplar = new List<(int Baslangic, int Bitis)>();

        for (var i = 1; i < noktalar.Count; i += grupBoyutu)
        {
            gruplar.Add((i, Math.Min(i + grupBoyutu, noktalar.Count)));
        }

        var toplam = 0d;

        Parallel.ForEach(
            gruplar,
            () => 0d,
            (grup, _, yerelToplam) =>
            {
                for (var i = grup.Baslangic; i < grup.Bitis; i++)
                {
                    yerelToplam += noktalar[i - 1].DistanceTo(noktalar[i]);
                }

                return yerelToplam;
            },
            yerel =>
            {
                // Paylaşılan toplam Interlocked ile birleştiriliyor (57. gün).
                // Düz `toplam += yerel` yazılsaydı aynı yarış durumu double
                // üzerinde oluşur ve sonuç her çalıştırmada değişirdi.
                double baslangic, yeni;

                do
                {
                    baslangic = toplam;
                    yeni = baslangic + yerel;
                }
                while (Math.Abs(Interlocked.CompareExchange(ref toplam, yeni, baslangic) - baslangic) > double.Epsilon);
            });

        return toplam;
    }

    /// <summary>İşi ÜÇ KEZ çalıştırıp MEDYAN süreyi döndürür.</summary>
    /// <remarks>
    /// Tek ölçüm, makinenin o anki yükü yüzünden yanıltıcı olabilir. Medyan,
    /// ortalamadan farklı olarak tek bir kötü turdan etkilenmiyor.
    /// </remarks>
    private static double Olc(Func<double> is_)
    {
        var olcumler = new List<double>(3);

        for (var i = 0; i < 3; i++)
        {
            var kronometre = Stopwatch.StartNew();
            _ = is_();
            kronometre.Stop();
            olcumler.Add(kronometre.Elapsed.TotalMilliseconds);
        }

        olcumler.Sort();

        return olcumler[1];
    }
}
