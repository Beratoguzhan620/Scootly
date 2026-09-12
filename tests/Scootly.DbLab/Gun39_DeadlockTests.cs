using Npgsql;
using Xunit.Abstractions;

namespace Scootly.DbLab;

/// <summary>
/// Gün 39 — kötümser kilitleme ve kasıtlı deadlock.
/// </summary>
/// <remarks>
/// Plan <c>Vehicle</c> ve <c>Wallet</c> tablolarını öneriyor; <c>Wallet</c>
/// henüz yazılmadı (Pricing context ileriki fazlarda). Deneyin gerektirdiği
/// tek şey iki ayrı kilitlenebilir tablo, o yüzden <c>Vehicles</c> ve
/// <c>Rides</c> kullanıldı. Anlatılan şey birebir aynı.
/// </remarks>
[Collection(LabCollection.Adi)]
public sealed class Gun39_DeadlockTests
{
    private readonly LabFixture _lab;
    private readonly ITestOutputHelper _cikti;

    public Gun39_DeadlockTests(LabFixture lab, ITestOutputHelper cikti)
    {
        _lab = lab;
        _cikti = cikti;
    }

    [Fact]
    public async Task Ters_kilit_sirasi_deadlock_uretir_ayni_sira_uretmez()
    {
        Assert.True(_lab.Hazir, _lab.AtlamaSebebi);

        // --- 1. TERS SIRA: deadlock bekliyoruz ---
        var aracId = await LabVeri.YeniMusaitAracAsync();
        var surusId = await LabVeri.YeniSurusAsync(aracId);

        var a = KilitleAsync(aracId, surusId, oncelikArac: true);
        var b = KilitleAsync(aracId, surusId, oncelikArac: false);

        var hatalar = await BeklemeHatalariniTopla(a, b);

        _cikti.WriteLine("=== TERS KILIT SIRASI ===");
        _cikti.WriteLine("Islem A: once Vehicles, sonra Rides");
        _cikti.WriteLine("Islem B: once Rides,    sonra Vehicles");
        foreach (var hata in hatalar)
        {
            _cikti.WriteLine($"  -> {hata}");
        }

        var deadlockOldu = hatalar.Exists(h => h.Contains("40P01", StringComparison.Ordinal));

        if (deadlockOldu)
        {
            _cikti.WriteLine("  PostgreSQL deadlock'u kendisi tespit etti (SQLSTATE 40P01)");
            _cikti.WriteLine("  ve taraflardan birini iptal ederek cozdu. Veritabani");
            _cikti.WriteLine("  bunu yapmasaydi iki islem birbirini sonsuza kadar beklerdi.");
        }
        else
        {
            _cikti.WriteLine("  Bu calistirmada deadlock olusmadi (zamanlamaya bagli).");
        }

        // --- 2. AYNI SIRA: deadlock beklemiyoruz ---
        var aracId2 = await LabVeri.YeniMusaitAracAsync();
        var surusId2 = await LabVeri.YeniSurusAsync(aracId2);

        var c = KilitleAsync(aracId2, surusId2, oncelikArac: true);
        var d = KilitleAsync(aracId2, surusId2, oncelikArac: true);

        var hatalar2 = await BeklemeHatalariniTopla(c, d);

        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("=== AYNI KILIT SIRASI ===");
        _cikti.WriteLine("Iki islem de: once Vehicles, sonra Rides");
        if (hatalar2.Count == 0)
        {
            _cikti.WriteLine("  Hata yok. Ikinci islem birincinin bitmesini bekledi");
            _cikti.WriteLine("  ve sirayla tamamlandilar.");
        }
        else
        {
            foreach (var hata in hatalar2)
            {
                _cikti.WriteLine($"  -> {hata}");
            }
        }

        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("Kural: birden fazla tablo kilitlenecekse HER YERDE ayni sirayla.");
        _cikti.WriteLine("Bu projede sira: once Vehicles, sonra Rides.");
        _cikti.WriteLine("Basit ama deadlock'u bastan imkansiz kilan bir disiplin.");

        // Ayni sirada deadlock OLMAMALI. Ters sirada olmasi zamanlamaya bagli,
        // o yuzden orada kesin bir iddia yok — ama ayni sirada olmasi bir hata.
        Assert.DoesNotContain(hatalar2, h => h.Contains("40P01", StringComparison.Ordinal));
    }

    private static async Task<List<string>> BeklemeHatalariniTopla(params Task[] isler)
    {
        var hatalar = new List<string>();

        foreach (var is_ in isler)
        {
            try
            {
                await is_;
            }
            catch (PostgresException ex)
            {
                hatalar.Add($"SQLSTATE {ex.SqlState}: {ex.MessageText}");
            }
        }

        return hatalar;
    }

    /// <summary>
    /// İki satırı <c>FOR UPDATE</c> ile kilitler. Aradaki gecikme, iki işlemin
    /// birbirinin kilidini bekleyeceği pencereyi açıyor.
    /// </summary>
    private static async Task KilitleAsync(Guid aracId, Guid surusId, bool oncelikArac)
    {
        await using var baglanti = await Lab.AcAsync();
        await using var islem = await baglanti.BeginTransactionAsync();

        if (oncelikArac)
        {
            await KilitleAracAsync(baglanti, islem, aracId);
            await Task.Delay(300);
            await KilitleSurusAsync(baglanti, islem, surusId);
        }
        else
        {
            await KilitleSurusAsync(baglanti, islem, surusId);
            await Task.Delay(300);
            await KilitleAracAsync(baglanti, islem, aracId);
        }

        await islem.CommitAsync();
    }

    private static async Task KilitleAracAsync(NpgsqlConnection baglanti, NpgsqlTransaction islem, Guid id)
    {
        await using var komut = new NpgsqlCommand(
            "SELECT 1 FROM \"Vehicles\" WHERE \"Id\" = @id FOR UPDATE", baglanti, islem);
        komut.Parameters.AddWithValue("id", id);
        await komut.ExecuteScalarAsync();
    }

    private static async Task KilitleSurusAsync(NpgsqlConnection baglanti, NpgsqlTransaction islem, Guid id)
    {
        await using var komut = new NpgsqlCommand(
            "SELECT 1 FROM \"Rides\" WHERE \"Id\" = @id FOR UPDATE", baglanti, islem);
        komut.Parameters.AddWithValue("id", id);
        await komut.ExecuteScalarAsync();
    }
}
