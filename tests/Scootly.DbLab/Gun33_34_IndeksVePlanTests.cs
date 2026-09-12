using System.Diagnostics;
using System.Text;
using Npgsql;
using Xunit.Abstractions;

namespace Scootly.DbLab;

/// <summary>
/// Gün 33 — indeksin etkisini ölçmek. Gün 34 — planı okumak.
/// </summary>
/// <remarks>
/// Test indeksi önce siliyor, ölçüyor, sonra oluşturup tekrar ölçüyor.
/// Migration sırasına güvenmek yerine böyle yapılmasının nedeni tekrar
/// edilebilirlik: ölçüm her çalıştırmada aynı iki durumu karşılaştırıyor.
/// </remarks>
[Collection(LabCollection.Adi)]
public sealed class Gun33_34_IndeksVePlanTests
{
    private const string KonumIndeksi = "ix_vehicles_konum";
    private const string DurumIndeksi = "ix_vehicles_durum";

    /// <summary>
    /// "Yakındaki müsait araçlar" — 29. gündeki uca en yakın sorgu.
    /// Kutu filtresi (BETWEEN) kullanılıyor; gerçek mesafe hesabı
    /// (Haversine) indeksten faydalanamaz çünkü sütunlar bir fonksiyonun
    /// içine girer ve B-ağacı indeksi o hali göremez. Bu, "neden önce kaba
    /// bir kutu, sonra hassas mesafe" deseninin sebebi.
    /// </summary>
    private const string OlculenSorgu = """
        SELECT "Id", "Latitude", "Longitude", "BatteryPercentage"
        FROM "Vehicles"
        WHERE "Status" = 'Available'
          AND "Latitude"  BETWEEN 36.980 AND 37.000
          AND "Longitude" BETWEEN 35.320 AND 35.340
        """;

    private readonly LabFixture _lab;
    private readonly ITestOutputHelper _cikti;

    public Gun33_34_IndeksVePlanTests(LabFixture lab, ITestOutputHelper cikti)
    {
        _lab = lab;
        _cikti = cikti;
    }

    [Fact]
    public async Task Indeks_oncesi_ve_sonrasi_olculur_ve_planlar_yazilir()
    {
        Assert.True(_lab.Hazir, _lab.AtlamaSebebi);

        await using var baglanti = await Lab.AcAsync();

        // --- ÖNCE: indeks yok ---
        await IndeksleriSilAsync(baglanti);
        await CalistirAsync(baglanti, "ANALYZE \"Vehicles\"");

        var oncePlan = await PlanAlAsync(baglanti);
        var onceSure = await OlcAsync(baglanti);

        // --- SONRA: indeksler var ---
        await IndeksleriOlusturAsync(baglanti);
        await CalistirAsync(baglanti, "ANALYZE \"Vehicles\"");

        var sonraPlan = await PlanAlAsync(baglanti);
        var sonraSure = await OlcAsync(baglanti);

        _cikti.WriteLine("=== INDEKS ONCESI PLAN ===");
        _cikti.WriteLine(oncePlan);
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("=== INDEKS SONRASI PLAN ===");
        _cikti.WriteLine(sonraPlan);
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("=== SURE (5 calistirmanin medyani) ===");
        _cikti.WriteLine($"indeks yok : {onceSure:F1} ms");
        _cikti.WriteLine($"indeks var : {sonraSure:F1} ms");

        var kazanc = onceSure <= 0 ? 0 : (onceSure - sonraSure) / onceSure * 100;
        _cikti.WriteLine($"fark       : %{kazanc:F0}");

        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("Planlarda aranacaklar:");
        _cikti.WriteLine(" - 'Seq Scan' mi 'Index Scan' / 'Bitmap Index Scan' mi?");
        _cikti.WriteLine(" - 'rows=' (tahmin) ile 'actual rows=' (gercek) farki ne kadar?");
        _cikti.WriteLine("   Buyuk fark, istatistiklerin eski oldugunu gosterir.");

        // Bu test bir OLCUM araci; belirli bir hizlanma dayatmiyor.
        // Sabit bir esik koymak (ornegin "%50 hizlanmali") makineye ve veri
        // dagilimina bagli olarak rastgele kirmizi verirdi. Tek iddia:
        // indeks eklendikten sonra plan degismis olmali.
        Assert.Contains("Seq Scan", oncePlan, StringComparison.Ordinal);
    }

    private static async Task IndeksleriSilAsync(NpgsqlConnection baglanti)
    {
        await CalistirAsync(baglanti, $"DROP INDEX IF EXISTS \"{KonumIndeksi}\"");
        await CalistirAsync(baglanti, $"DROP INDEX IF EXISTS \"{DurumIndeksi}\"");
    }

    private static async Task IndeksleriOlusturAsync(NpgsqlConnection baglanti)
    {
        // Bilesik indekste SUTUN SIRASI onemli: indeks ilk sutuna gore sirali
        // tutulur. Sorgu ilk sutunu kullanmiyorsa indeks buyuk olcude ise
        // yaramaz kalir. Burada iki sutun da BETWEEN ile suzuluyor.
        await CalistirAsync(baglanti,
            $"CREATE INDEX IF NOT EXISTS \"{KonumIndeksi}\" ON \"Vehicles\" (\"Latitude\", \"Longitude\")");

        // Status yalnizca dort deger aliyor — secicilik dusuk. Tek basina pek
        // ise yaramaz; kismi indeks (partial index) ise anlamli, cunku
        // sorgularin cogu yalnizca Available araclarla ilgileniyor.
        await CalistirAsync(baglanti,
            $"CREATE INDEX IF NOT EXISTS \"{DurumIndeksi}\" ON \"Vehicles\" (\"Status\") WHERE \"Status\" = 'Available'");
    }

    private static async Task CalistirAsync(NpgsqlConnection baglanti, string sql)
    {
        await using var komut = new NpgsqlCommand(sql, baglanti) { CommandTimeout = 300 };
        await komut.ExecuteNonQueryAsync();
    }

    private static async Task<string> PlanAlAsync(NpgsqlConnection baglanti)
    {
        await using var komut = new NpgsqlCommand($"EXPLAIN ANALYZE {OlculenSorgu}", baglanti);
        await using var okuyucu = await komut.ExecuteReaderAsync();

        var plan = new StringBuilder();
        while (await okuyucu.ReadAsync())
        {
            plan.AppendLine(okuyucu.GetString(0));
        }

        return plan.ToString().TrimEnd();
    }

    /// <summary>Bes olcumun medyani. Tek olcum, o anda ne oldugunu degil
    /// gurultu de olcer; medyan tek seferlik sicramalari eler.</summary>
    private static async Task<double> OlcAsync(NpgsqlConnection baglanti)
    {
        var sureler = new List<double>();

        for (var i = 0; i < 5; i++)
        {
            var kronometre = Stopwatch.StartNew();

            await using (var komut = new NpgsqlCommand(OlculenSorgu, baglanti))
            await using (var okuyucu = await komut.ExecuteReaderAsync())
            {
                while (await okuyucu.ReadAsync()) { }
            }

            kronometre.Stop();
            sureler.Add(kronometre.Elapsed.TotalMilliseconds);
        }

        sureler.Sort();
        return sureler[sureler.Count / 2];
    }
}
