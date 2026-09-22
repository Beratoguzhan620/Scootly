using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Scootly.DbLab;

/// <summary>
/// Gün 41 — değişiklik izleyicisinin okuma sorgularındaki maliyeti.
/// </summary>
/// <remarks>
/// Ölçülen şey iki kat: geçen süre ve tahsis edilen bellek. İkisini birden
/// ölçmek önemli, çünkü izleyicinin asıl maliyeti bellekte — süre farkı küçük
/// bir makinede gürültüye karışabilir ama tahsis farkı kararlı.
/// </remarks>
[Collection(LabCollection.Adi)]
public sealed class Gun41_TakipMaliyetiTests
{
    private const int Tekrar = 1_000;
    private const int SayfaBoyutu = 100;

    private readonly LabFixture _lab;
    private readonly ITestOutputHelper _cikti;

    public Gun41_TakipMaliyetiTests(LabFixture lab, ITestOutputHelper cikti)
    {
        _lab = lab;
        _cikti = cikti;
    }

    [Fact]
    public async Task Takipli_ve_takipsiz_sorgu_karsilastirilir()
    {
        Assert.True(_lab.Hazir, _lab.AtlamaSebebi);

        // Isınma: ilk sorgu model kurulumunu ve bağlantı havuzunun dolmasını
        // da ölçerdi; o maliyet karşılaştırmanın konusu değil.
        await TakipsizAsync(10);
        await TakipliAsync(10);

        var (takipsizSure, takipsizBayt) = await OlcAsync(() => TakipsizAsync(Tekrar));
        var (takipliSure, takipliBayt) = await OlcAsync(() => TakipliAsync(Tekrar));

        _cikti.WriteLine($"Tekrar        : {Tekrar:N0} sorgu");
        _cikti.WriteLine($"Sayfa boyutu  : {SayfaBoyutu} satir");
        _cikti.WriteLine(new string('=', 60));
        _cikti.WriteLine("Sorgu             |    Sure (ms) |     Tahsis (MB)");
        _cikti.WriteLine(new string('-', 60));
        _cikti.WriteLine($"{"AsNoTracking",-17} | {takipsizSure,12:N0} | {takipsizBayt / 1048576d,15:N1}");
        _cikti.WriteLine($"{"AsTracking",-17} | {takipliSure,12:N0} | {takipliBayt / 1048576d,15:N1}");
        _cikti.WriteLine(new string('=', 60));
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("Takipli sorguda EF her satirin bir KOPYASINI daha tutuyor:");
        _cikti.WriteLine("SaveChanges cagrildiginda neyin degistigini o kopyayla");
        _cikti.WriteLine("karsilastirarak buluyor. Okuma sorgusunda bu kopya hicbir");
        _cikti.WriteLine("zaman kullanilmiyor.");
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("Bu yuzden ScootlyDbContext'in VARSAYILANI takipsiz (ADR 0016)");
        _cikti.WriteLine("ve yazma yollari acikca AsTracking() diyor. Tersi olsaydi,");
        _cikti.WriteLine("unutulan her okuma sorgusu bu maliyeti SESSIZCE odetirdi.");

        Assert.True(
            takipliBayt > takipsizBayt,
            $"Takipli sorgu ({takipliBayt:N0} bayt) takipsizden ({takipsizBayt:N0} bayt) " +
            "daha az tahsis etti — olcum hatali olmali.");
    }

    private static async Task TakipsizAsync(int tekrar)
    {
        await using var db = Lab.Context();

        for (var i = 0; i < tekrar; i++)
        {
            // Varsayilan zaten takipsiz (41. gun); acikca yazmak, testin neyi
            // olctugunu okunur birakiyor.
            _ = await db.Vehicles
                .AsNoTracking()
                .OrderBy(v => v.Id)
                .Take(SayfaBoyutu)
                .ToListAsync();
        }
    }

    private static async Task TakipliAsync(int tekrar)
    {
        for (var i = 0; i < tekrar; i++)
        {
            // HER TEKRAR KENDI BAGLAMINI aciyor. Tek bir baglamda dongu
            // kurulsaydi izleyici tur tur buyur ve olculen sey takip maliyeti
            // degil, giderek dolan bir izleyicinin cokusu olurdu — gercekci
            // degil, cunku uretimde her istek kendi baglamini alir.
            await using var db = Lab.Context();

            _ = await db.Vehicles
                .AsTracking()
                .OrderBy(v => v.Id)
                .Take(SayfaBoyutu)
                .ToListAsync();
        }
    }

    private static async Task<(long Sure, long Bayt)> OlcAsync(Func<Task> is_)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var oncekiBayt = GC.GetTotalAllocatedBytes(precise: true);
        var kronometre = Stopwatch.StartNew();

        await is_();

        kronometre.Stop();
        var sonrakiBayt = GC.GetTotalAllocatedBytes(precise: true);

        return (kronometre.ElapsedMilliseconds, sonrakiBayt - oncekiBayt);
    }
}
