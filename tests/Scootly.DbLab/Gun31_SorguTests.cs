using System.Text;
using Npgsql;
using Xunit.Abstractions;

namespace Scootly.DbLab;

/// <summary>
/// Gün 31 — doğrudan SQL ile çalışma.
/// Sorgular <c>sql/02-dogrulama-sorgulari.sql</c> dosyasında; burada
/// çalıştırılıp sonuçları ekrana basılıyor.
/// </summary>
[Collection(LabCollection.Adi)]
public sealed class Gun31_SorguTests
{
    private readonly LabFixture _lab;
    private readonly ITestOutputHelper _cikti;

    public Gun31_SorguTests(LabFixture lab, ITestOutputHelper cikti)
    {
        _lab = lab;
        _cikti = cikti;
    }

    [Fact]
    public async Task On_dogrulama_sorgusu_calisir_ve_sonuc_dondurur()
    {
        // Veritabani yoksa test ATLANMIYOR, KIRILIYOR. Sessiz atlama,
        // bozuk bir ortamin fark edilmeden gecmesine yol acar.
        Assert.True(_lab.Hazir, _lab.AtlamaSebebi);

        await using var baglanti = await Lab.AcAsync();

        var aracSayisi = Convert.ToInt64(
            await Lab.SkalerAsync(baglanti, "SELECT COUNT(*) FROM \"Vehicles\"") ?? 0L);
        var surusSayisi = Convert.ToInt64(
            await Lab.SkalerAsync(baglanti, "SELECT COUNT(*) FROM \"Rides\"") ?? 0L);

        _cikti.WriteLine($"Arac: {aracSayisi:N0}   Surus: {surusSayisi:N0}");
        _cikti.WriteLine(new string('=', 70));

        var calisan = 0;

        foreach (var (baslik, sorgu) in SorgulariAyikla(Lab.SqlOku("02-dogrulama-sorgulari.sql")))
        {
            _cikti.WriteLine(string.Empty);
            _cikti.WriteLine(baslik);
            _cikti.WriteLine(new string('-', baslik.Length));

            var basladi = DateTime.UtcNow;
            var tablo = await SorguCalistirAsync(baglanti, sorgu);
            var sure = DateTime.UtcNow - basladi;

            _cikti.WriteLine(tablo);
            _cikti.WriteLine($"({sure.TotalMilliseconds:F0} ms)");
            calisan++;
        }

        // Dosyada on sorgu var; biri silinir veya bozulursa bu satir yakalar.
        Assert.Equal(10, calisan);
        Assert.True(aracSayisi >= 100_000, $"Beklenen en az 100.000 arac, bulunan {aracSayisi}");
    }

    /// <summary>
    /// "-- ADI: ..." yorumuyla baslayan bloklari ayirir.
    /// </summary>
    private static IEnumerable<(string Baslik, string Sorgu)> SorgulariAyikla(string dosya)
    {
        string? baslik = null;
        var govde = new StringBuilder();

        foreach (var satir in dosya.Split('\n'))
        {
            var kirpilmis = satir.TrimEnd('\r');

            if (kirpilmis.TrimStart().StartsWith("-- ADI:", StringComparison.Ordinal))
            {
                if (baslik is not null && govde.Length > 0)
                {
                    yield return (baslik, govde.ToString());
                }

                baslik = kirpilmis.TrimStart()[7..].Trim();
                govde.Clear();
                continue;
            }

            if (baslik is not null)
            {
                govde.AppendLine(kirpilmis);
            }
        }

        if (baslik is not null && govde.Length > 0)
        {
            yield return (baslik, govde.ToString());
        }
    }

    private static async Task<string> SorguCalistirAsync(NpgsqlConnection baglanti, string sorgu)
    {
        await using var komut = new NpgsqlCommand(sorgu, baglanti) { CommandTimeout = 120 };
        await using var okuyucu = await komut.ExecuteReaderAsync();

        var satirlar = new StringBuilder();
        var basliklar = new List<string>();

        for (var i = 0; i < okuyucu.FieldCount; i++)
        {
            basliklar.Add(okuyucu.GetName(i));
        }

        satirlar.AppendLine(string.Join(" | ", basliklar));

        var adet = 0;
        while (await okuyucu.ReadAsync() && adet < 15)
        {
            var degerler = new List<string>();
            for (var i = 0; i < okuyucu.FieldCount; i++)
            {
                degerler.Add(okuyucu.IsDBNull(i) ? "NULL" : okuyucu.GetValue(i).ToString() ?? string.Empty);
            }

            satirlar.AppendLine(string.Join(" | ", degerler));
            adet++;
        }

        return satirlar.ToString().TrimEnd();
    }
}
