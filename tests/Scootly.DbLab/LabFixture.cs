using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Scootly.DbLab;

/// <summary>
/// Laboratuvar veritabanını bir kez oluşturur, migration'ları uygular ve
/// 100 bin satırlık örnek veriyi yükler.
/// </summary>
public sealed class LabFixture : IAsyncLifetime
{
    public bool Hazir { get; private set; }

    public string AtlamaSebebi { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (!Lab.Yapilandirildi)
        {
            AtlamaSebebi = Lab.YapilandirmaUyarisi;
            return;
        }

        try
        {
            await VeritabaniniOlusturAsync();
        }
        catch (NpgsqlException ex)
        {
            // Sunucu ayakta değilse testler kirmizi degil ATLANMIS olmali:
            // kodda bir hata yok, ortam eksik. İkisini karıştırmak, gerçek bir
            // regresyonun "ortam sorunu" diye geçiştirilmesine yol açar.
            AtlamaSebebi = $"Postgres'e baglanilamadi: {ex.Message}";
            return;
        }

        await using (var db = Lab.Context())
        {
            await db.Database.MigrateAsync();
        }

        await OrnekVeriYukleAsync();
        Hazir = true;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task VeritabaniniOlusturAsync()
    {
        await using var yonetim = new NpgsqlConnection(Lab.YonetimBaglantisi);
        await yonetim.OpenAsync();

        await using var varMi = new NpgsqlCommand(
            "SELECT 1 FROM pg_database WHERE datname = @ad", yonetim);
        varMi.Parameters.AddWithValue("ad", Lab.VeritabaniAdi);

        if (await varMi.ExecuteScalarAsync() is not null)
        {
            return;
        }

        // CREATE DATABASE parametre kabul etmez; ad sabit bir sabitten geliyor,
        // kullanıcı girdisinden değil, bu yüzden burada enjeksiyon riski yok.
        await using var olustur = new NpgsqlCommand(
            $"CREATE DATABASE \"{Lab.VeritabaniAdi}\"", yonetim);
        await olustur.ExecuteNonQueryAsync();
    }

    private static async Task OrnekVeriYukleAsync()
    {
        await using var baglanti = await Lab.AcAsync();

        var mevcut = Convert.ToInt64(
            await Lab.SkalerAsync(baglanti, "SELECT COUNT(*) FROM \"Vehicles\"") ?? 0L);

        if (mevcut >= 100_000)
        {
            return;
        }

        await using var komut = new NpgsqlCommand(Lab.SqlOku("01-ornek-veri.sql"), baglanti);
        komut.CommandTimeout = 300;
        await komut.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(Adi)]
public sealed class LabCollection : ICollectionFixture<LabFixture>
{
    public const string Adi = "scootly-lab";
}
