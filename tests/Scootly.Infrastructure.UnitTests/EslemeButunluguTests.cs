using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Scootly.Domain.Fleet;
using Scootly.Domain.Riding;
using Scootly.Domain.Telemetry;
using Scootly.Infrastructure.Persistence;

namespace Scootly.Infrastructure.UnitTests;

/// <summary>
/// Alan modelindeki her genel özelliğin gerçekten bir sütuna karşılık geldiğini
/// doğrular.
/// </summary>
/// <remarks>
/// <para>
/// Bu testin var olma sebebi somut bir hata: <c>Ride.StartedAt</c> salt okunur
/// (<c>{ get; }</c>) tanımlı olduğu için EF Core'un varsayılan kuralı onu hiç
/// eşlemedi ve alan aylarca hiçbir migration'a girmedi. Derleme temizdi, bütün
/// birim testler yeşildi — çünkü hiçbiri veritabanına gitmiyordu. Hata ancak
/// migration dosyası elle okunduğunda görüldü.
/// </para>
/// <para>
/// Böyle bir hatayı yakalamanın doğru yeri tek tek alanlar için yazılmış
/// iddialar değil: bir sonraki salt okunur özelliği yine kimse fark etmezdi.
/// Doğru yer, modelin KENDİSİNİ sorgulamak. Aşağıdaki test alan tiplerini
/// yansımayla (reflection) geziyor ve EF'in eşlediği özelliklerle
/// karşılaştırıyor; yeni bir alan eklendiğinde ve eşlenmediğinde kırmızı oluyor.
/// </para>
/// <para>
/// Test veritabanına bağlanmıyor. <c>UseNpgsql</c> bir bağlantı dizesi alıyor
/// ama model kurulurken sunucuya gidilmiyor — ölçülen şey şema değil, EF'in
/// bellekte kurduğu model.
/// </para>
/// </remarks>
public sealed class EslemeButunluguTests
{
    /// <summary>
    /// Kasıtlı olarak sütuna dönüşmemesi gereken özellikler.
    /// Listeye ekleme yapmak bir karardır; boş bırakmak da bir karardır.
    /// </summary>
    private static readonly HashSet<string> BilincliDisaridakiler = new(StringComparer.Ordinal)
    {
        // AggregateRoot.DomainEvents: olaylar yayımlandıktan sonra atılıyor,
        // kalıcı değil. Kalıcı olsaydı outbox tablosu olurdu, sütun değil.
        "DomainEvents"
    };

    private static ScootlyDbContext Baglam()
    {
        var secenekler = new DbContextOptionsBuilder<ScootlyDbContext>()
            .UseNpgsql("Host=eslesme-testi;Database=yok;Username=yok;Password=yok")
            .Options;

        return new ScootlyDbContext(secenekler);
    }

    [Theory]
    [InlineData(typeof(Ride))]
    [InlineData(typeof(Vehicle))]
    [InlineData(typeof(TelemetryReading))]
    public void Alan_tipinin_her_genel_ozelligi_eslenmis_olmali(Type alanTipi)
    {
        using var db = Baglam();

        var varlik = db.Model.FindEntityType(alanTipi);
        Assert.NotNull(varlik);

        // Sütunlar + sahip olunan tipler (GeoPoint gibi) + gölge özellikler.
        var eslenmis = varlik!.GetProperties().Select(p => p.Name)
            .Concat(varlik.GetNavigations().Select(n => n.Name))
            .ToHashSet(StringComparer.Ordinal);

        var eksikler = alanTipi
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .Select(p => p.Name)
            .Where(ad => !BilincliDisaridakiler.Contains(ad))
            .Where(ad => !eslenmis.Contains(ad))
            .OrderBy(ad => ad, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            eksikler.Count == 0,
            $"{alanTipi.Name} uzerinde su ozellikler hicbir sutuna eslenmemis: " +
            $"{string.Join(", ", eksikler)}. " +
            "Salt okunur ({ get; }) ozellikler EF Core kuralla eslenmez; " +
            $"{alanTipi.Name}Configuration icinde acikca Property(...) yazilmali. " +
            "Eslenmemesi DOGRUYSA, sebebiyle birlikte BilincliDisaridakiler'e ekle.");
    }

    [Fact]
    public void Ride_baslangic_zamani_sutun_olarak_var()
    {
        using var db = Baglam();

        var ozellik = db.Model.FindEntityType(typeof(Ride))!.FindProperty(nameof(Ride.StartedAt));

        // Yukaridaki genel test bunu zaten kapsiyor. Bu ikinci test, hatanin
        // kendisini ismiyle kayda geciriyor: bir daha olursa kirmizi olan testin
        // adi ne oldugunu soyluyor.
        Assert.NotNull(ozellik);
        Assert.False(ozellik!.IsNullable);
    }
}
