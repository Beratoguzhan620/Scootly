using Microsoft.EntityFrameworkCore;
using Npgsql;
using Scootly.Infrastructure.Persistence;

namespace Scootly.DbLab;

/// <summary>
/// Ölçüm laboratuvarının ortak altyapısı.
/// </summary>
/// <remarks>
/// <para>
/// Bu testler Testcontainers KULLANMIYOR. Geliştirme makinesi Apple Silicon
/// üzerinde Parallels ile çalışan bir Windows sanal makinesi; Docker macOS
/// tarafında duruyor ve misafirden erişilemiyor. Docker Desktop'ın
/// "Expose daemon on tcp://" seçeneği bunu çözerdi ama daemon'ı kimlik
/// doğrulamasız açar — o porta ulaşan herkes ana makinede root yetkisine
/// sahip olur. Bu bedel, hermetik test uğruna ödenmeye değmez.
/// </para>
/// <para>
/// Onun yerine testler çalışan Postgres'e bağlanıp <b>kendi ayrı
/// veritabanlarını</b> oluşturuyor. Bedeli: testler hermetik değil, aynı
/// sunucuyu paylaşıyorlar ve sunucu ayakta değilse atlanıyorlar. Kazancı:
/// ölçümler gerçek bir PostgreSQL üzerinde yapılıyor.
/// </para>
/// </remarks>
public static class Lab
{
    public const string VeritabaniAdi = "scootly_lab";

    /// <summary>Sunucuya bağlanmak için (veritabanı oluşturmak amacıyla).</summary>
    public static string? YonetimBaglantisi =>
        Environment.GetEnvironmentVariable("SCOOTLY_LAB_ADMIN");

    /// <summary>Laboratuvar veritabanına bağlanmak için.</summary>
    public static string? LabBaglantisi =>
        Environment.GetEnvironmentVariable("SCOOTLY_LAB_DB");

    public static bool Yapilandirildi =>
        !string.IsNullOrWhiteSpace(YonetimBaglantisi) && !string.IsNullOrWhiteSpace(LabBaglantisi);

    public const string YapilandirmaUyarisi =
        "SCOOTLY_LAB_ADMIN ve SCOOTLY_LAB_DB ortam degiskenleri tanimli degil. " +
        "Once 'bash lab.sh' calistir.";

    public static ScootlyDbContext Context()
    {
        var options = new DbContextOptionsBuilder<ScootlyDbContext>()
            .UseNpgsql(LabBaglantisi)
            .Options;

        return new ScootlyDbContext(options);
    }

    public static async Task<NpgsqlConnection> AcAsync(CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(LabBaglantisi);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    /// <summary>sql/ klasöründeki bir dosyanın içeriğini okur.</summary>
    public static string SqlOku(string dosyaAdi) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "sql", dosyaAdi));

    /// <summary>Tek bir skaler değer döndüren sorgu çalıştırır.</summary>
    public static async Task<object?> SkalerAsync(
        NpgsqlConnection connection, string sql, CancellationToken cancellationToken = default)
    {
        await using var komut = new NpgsqlCommand(sql, connection);
        return await komut.ExecuteScalarAsync(cancellationToken);
    }
}
