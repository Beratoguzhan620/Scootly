using Scootly.Application.Telemetry;
using Scootly.Domain.Geo;
using Scootly.Domain.Telemetry;
using Xunit.Abstractions;

namespace Scootly.DbLab;

/// <summary>
/// Gün 57 — bellekteki yarış durumu ve <c>Interlocked</c>.
/// </summary>
/// <remarks>
/// <para>
/// 36. günde veritabanı seviyesindeki yarışı ölçmüştük. Bu ayrı bir problem:
/// burada veritabanı yok, kilit yok, yalnızca bellek var. İki iş parçacığı
/// aynı <c>int</c> değişkeni artırdığında sonuç beklenenden küçük çıkıyor —
/// çünkü <c>sayac++</c> tek bir işlem değil, ÜÇ işlem: oku, bir ekle, yaz.
/// İki iş parçacığı aynı değeri okursa ikisi de aynı sonucu yazar ve bir
/// artış kaybolur.
/// </para>
/// </remarks>
[Collection(EszamanlilikKoleksiyonu.Adi)]
public sealed class Gun57_IsParcacigiGuvenligiTests
{
    private const int IsParcacigi = 8;
    private const int ArtisSayisi = 100_000;

    private readonly ITestOutputHelper _cikti;

    private int _korumasizSayac;
    private int _korumaliSayac;

    public Gun57_IsParcacigiGuvenligiTests(ITestOutputHelper cikti)
    {
        _cikti = cikti;
    }

    [Fact]
    public async Task Korumasiz_artirma_yanlis_sonuc_verir_Interlocked_dogru()
    {
        var beklenen = IsParcacigi * ArtisSayisi;

        await Task.WhenAll(Enumerable.Range(0, IsParcacigi).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < ArtisSayisi; i++)
            {
                // KORUMASIZ: oku, bir ekle, yaz. Arada baska bir is parcacigi
                // ayni degeri okuyabilir.
                _korumasizSayac++;

                // KORUMALI: tek, bolunemez (atomik) islem.
                Interlocked.Increment(ref _korumaliSayac);
            }
        })));

        var kayip = beklenen - _korumasizSayac;

        _cikti.WriteLine($"Is parcacigi        : {IsParcacigi}");
        _cikti.WriteLine($"Her birinin artisi  : {ArtisSayisi:N0}");
        _cikti.WriteLine($"Beklenen            : {beklenen:N0}");
        _cikti.WriteLine(new string('-', 46));
        _cikti.WriteLine($"Korumasiz (sayac++) : {_korumasizSayac:N0}  (kayip: {kayip:N0})");
        _cikti.WriteLine($"Interlocked         : {_korumaliSayac:N0}");
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("Kaybin buyuklugu makineye ve o anki yuke gore degisir —");
        _cikti.WriteLine("bu yuzden 'sifir kayip' goren bir calistirma, kodun dogru");
        _cikti.WriteLine("oldugunu DEGIL, yarisin o sefer olusmadigini gosterir.");
        _cikti.WriteLine("Eszamanlilik hatalarinin sinsiligi tam olarak bu.");

        Assert.Equal(beklenen, _korumaliSayac);

        // Korumasiz sayac icin "yanlis olmali" diye iddia EDILMIYOR: yaris
        // olusmayabilir ve test o zaman sahte bir basarisizlik uretirdi.
        // Iddia edilen sey, korumalinin HER ZAMAN dogru olmasi.
        Assert.True(
            _korumasizSayac <= beklenen,
            "Korumasiz sayac beklenenden buyuk cikamaz; cikmissa olcum hatali.");
    }

    [Fact]
    public void Telemetri_kanalinda_atilan_kayit_sayaci_dogru()
    {
        // Kanalı kapasitesinin çok üstünde doldurup atılanları sayıyoruz.
        // Bu sayaç gerçek koddaki paylaşılan durum — 52. günde eklendi ve
        // Interlocked ile korunuyor.
        var kanal = new TelemetryChannel();
        var fazlalik = 500;
        var toplam = TelemetryChannel.Kapasite + fazlalik;

        var olcumler = Enumerable.Range(0, toplam)
            .Select(_ => new TelemetryReading(
                Guid.NewGuid(),
                new DeviceId("sim-0001"),
                new GeoPoint(37.0, 35.3),
                50,
                DateTime.UtcNow))
            .ToList();

        var atilan = kanal.Yaz(olcumler);

        _cikti.WriteLine($"Kapasite      : {TelemetryChannel.Kapasite:N0}");
        _cikti.WriteLine($"Yazilan       : {toplam:N0}");
        _cikti.WriteLine($"Atilan        : {atilan:N0}");
        _cikti.WriteLine($"Sayacta duran : {kanal.AtilanKayitSayisi:N0}");
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("Atilan kayit SESSIZ DEGIL: sayilip hem yanitta hem logda");
        _cikti.WriteLine("goruluyor. Fark edilmeyen veri kaybi, kaybin kendisinden");
        _cikti.WriteLine("daha tehlikeli.");

        Assert.Equal(fazlalik, atilan);
        Assert.Equal(fazlalik, kanal.AtilanKayitSayisi);
    }
}
