using System.Diagnostics;
using Xunit.Abstractions;

namespace Scootly.DbLab;

/// <summary>
/// Gün 56 — async gerçekte ne yapıyor, <c>.Result</c> neyi bozuyor.
/// </summary>
/// <remarks>
/// <para>
/// Veritabanı GEREKMİYOR; bu deneyler .NET çalışma zamanını ölçüyor.
/// <see cref="LabFixture"/> kullanılmamasının sebebi bu.
/// </para>
/// <para>
/// Kendi koleksiyonunda, çünkü iş parçacığı havuzunu doyuruyor. Diğer
/// testlerle paralel çalışsaydı onların sürelerini de bozar ve ölçüm anlamsız
/// hale gelirdi.
/// </para>
/// </remarks>
[Collection(EszamanlilikKoleksiyonu.Adi)]
public sealed class Gun56_AsyncVeIsParcacigiTests
{
    /// <summary>
    /// Havuzun başlangıçtaki iş parçacığı sayısından belirgin biçimde fazla.
    /// </summary>
    private const int EszamanliIs = 256;

    private static readonly TimeSpan IsSuresi = TimeSpan.FromMilliseconds(50);

    private readonly ITestOutputHelper _cikti;

    public Gun56_AsyncVeIsParcacigiTests(ITestOutputHelper cikti)
    {
        _cikti = cikti;
    }

    [Fact]
    public async Task Bloklayan_cagri_await_edilenden_belirgin_olcude_yavas()
    {
        ThreadPool.GetMinThreads(out var minIsParcacigi, out _);

        // --- DOĞRU: await ---
        // Bekleme anında iş parçacığı BLOKLANMIYOR, havuza geri dönüyor ve
        // başka bir işe hizmet ediyor. 256 iş, birkaç iş parçacığıyla
        // neredeyse eşzamanlı ilerliyor.
        var dogruKronometre = Stopwatch.StartNew();
        await Task.WhenAll(Enumerable.Range(0, EszamanliIs).Select(_ => DogruAsync()));
        dogruKronometre.Stop();

        var zirveSonrasi = IsParcacigiSayisi();

        // --- YANLIŞ: .Result / .Wait() ---
        // Bekleme boyunca iş parçacığı TUTULUYOR. 256 iş için 256 iş parçacığı
        // gerekiyor ama havuzda o kadar yok; havuz yenilerini saniyede birkaç
        // tane hızında ekliyor. Buna "iş parçacığı havuzu açlığı" deniyor.
        var yanlisKronometre = Stopwatch.StartNew();
        await Task.WhenAll(Enumerable.Range(0, EszamanliIs)
            .Select(_ => Task.Run(YanlisSenkron)));
        yanlisKronometre.Stop();

        _cikti.WriteLine($"Es zamanli is       : {EszamanliIs}");
        _cikti.WriteLine($"Her isin suresi     : {IsSuresi.TotalMilliseconds:0} ms");
        _cikti.WriteLine($"Havuz min is parcasi: {minIsParcacigi}");
        _cikti.WriteLine(new string('-', 52));
        _cikti.WriteLine($"await ile           : {dogruKronometre.ElapsedMilliseconds,6} ms");
        _cikti.WriteLine($".Result ile         : {yanlisKronometre.ElapsedMilliseconds,6} ms");
        _cikti.WriteLine($"Zirvedeki is parcasi: {zirveSonrasi}");
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("Okuma notu: async bir metot ARKA PLANDA BIR IS PARCACIGI");
        _cikti.WriteLine("CALISTIRMAZ. Yaptigi sey, bekleme suresince is parcacigini");
        _cikti.WriteLine("havuza geri vermek. .Result bu geri vermeyi engelliyor ve");
        _cikti.WriteLine("her bekleyen istek bir is parcacigi tutuyor.");
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("Uretimde bunun gorunumu su: yuk arttikca yanit sureleri");
        _cikti.WriteLine("sicriyor ama CPU dusuk. Kimse calismiyor, herkes bekliyor.");

        Assert.True(
            yanlisKronometre.ElapsedMilliseconds > dogruKronometre.ElapsedMilliseconds,
            $"Bloklayan surum ({yanlisKronometre.ElapsedMilliseconds} ms) await edilenden " +
            $"({dogruKronometre.ElapsedMilliseconds} ms) yavas cikmadi. " +
            "Makinede cok cekirdek varsa havuz acligi olusmamis olabilir.");
    }

    private static async Task DogruAsync() => await Task.Delay(IsSuresi);

    private static void YanlisSenkron()
    {
        // BU BIR ORNEK, TAVSIYE DEGIL. Asenkron bir metodu senkron beklemek
        // hem is parcacigi tutar hem de bazi baglamlarda kilitlenmeye yol acar.
#pragma warning disable xUnit1031
        Task.Delay(IsSuresi).GetAwaiter().GetResult();
#pragma warning restore xUnit1031
    }

    private static int IsParcacigiSayisi()
    {
        ThreadPool.GetMaxThreads(out var maxIsParcacigi, out _);
        ThreadPool.GetAvailableThreads(out var bosIsParcacigi, out _);
        return maxIsParcacigi - bosIsParcacigi;
    }
}

/// <summary>
/// İş parçacığı havuzunu doyuran deneyler bu koleksiyonda toplanıyor.
/// </summary>
/// <remarks>
/// xUnit farklı koleksiyonları paralel çalıştırır. Bu deneyler zamana
/// bakıyor; başka bir testle aynı anda koşmaları ölçümü bozar.
/// </remarks>
[CollectionDefinition(Adi, DisableParallelization = true)]
public sealed class EszamanlilikKoleksiyonu
{
    public const string Adi = "eszamanlilik-deneyleri";
}
