using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Scootly.Api.Authorization;
using Scootly.Domain.Common;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding;
using Scootly.Infrastructure.Identity;

namespace Scootly.Api.UnitTests;

/// <summary>
/// 24. günün asıl iddiasını sınar: karar, kullanıcıya değil kullanıcı ile
/// KAYNAK arasındaki ilişkiye bakıyor. Veritabanı, web sunucusu ve Docker
/// gerektirmez.
/// </summary>
public sealed class ResourcePolicyTests
{
    private static readonly Guid SurucuA = Guid.Parse("aaaaaaaa-1111-1111-1111-aaaaaaaaaaaa");
    private static readonly Guid SurucuB = Guid.Parse("bbbbbbbb-2222-2222-2222-bbbbbbbbbbbb");

    private sealed class SahaGorevi : IRegionScoped
    {
        public SahaGorevi(string bolge) => Region = bolge;

        public string Region { get; }
    }

    private static ServiceProvider Kur()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScootlyAuthorization();
        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal Kullanici(Guid? kimlik = null, string? rol = null, string? bolge = null)
    {
        var iddialar = new List<Claim>();

        if (kimlik is not null)
        {
            iddialar.Add(new Claim(ScootlyClaimTypes.Subject, kimlik.Value.ToString()));
        }

        if (rol is not null)
        {
            iddialar.Add(new Claim(ScootlyClaimTypes.Role, rol));
        }

        if (bolge is not null)
        {
            iddialar.Add(new Claim(ScootlyClaimTypes.HomeRegion, bolge));
        }

        var kimlikNesnesi = new ClaimsIdentity(
            iddialar,
            authenticationType: "Test",
            nameType: ScootlyClaimTypes.Subject,
            roleType: ScootlyClaimTypes.Role);

        return new ClaimsPrincipal(kimlikNesnesi);
    }

    private static Ride Surus(Guid surucuId) => new(
        RideId.New(),
        surucuId,
        Guid.NewGuid(),
        new GeoPoint(36.99, 35.32),
        new DateTime(2026, 9, 12, 10, 0, 0, DateTimeKind.Utc));

    // --- Sürüş sahipliği ---

    [Fact]
    public async Task Surucu_kendi_surusune_erisebilir()
    {
        using var saglayici = Kur();
        var yetki = saglayici.GetRequiredService<IAuthorizationService>();

        var sonuc = await yetki.AuthorizeAsync(
            Kullanici(SurucuA, RoleNames.Driver), Surus(SurucuA), PolicyNames.SurusSahibi);

        Assert.True(sonuc.Succeeded);
    }

    [Fact]
    public async Task Surucu_baskasinin_surusune_erisemez()
    {
        using var saglayici = Kur();
        var yetki = saglayici.GetRequiredService<IAuthorizationService>();

        // Planın 24. gün için istediği test: sürücü A, sürücü B'nin sürüşüne
        // erişmeye çalışıyor. Rolü doğru (sürücü), kimliği doğrulanmış —
        // ve yine de geçemiyor. Rol tabanlı yetkinin yakalayamadığı durum bu.
        var sonuc = await yetki.AuthorizeAsync(
            Kullanici(SurucuA, RoleNames.Driver), Surus(SurucuB), PolicyNames.SurusSahibi);

        Assert.False(sonuc.Succeeded);
    }

    [Fact]
    public async Task Yonetici_rolu_tek_basina_baskasinin_surusunu_acmaz()
    {
        using var saglayici = Kur();
        var yetki = saglayici.GetRequiredService<IAuthorizationService>();

        // Yetkili bir rol, sahiplik kuralını otomatik olarak aşmaz.
        // Aşmasını istiyorsak bunu AYRI bir handler olarak, bilerek yazmalıyız.
        var sonuc = await yetki.AuthorizeAsync(
            Kullanici(SurucuA, RoleNames.FleetManager), Surus(SurucuB), PolicyNames.SurusSahibi);

        Assert.False(sonuc.Succeeded);
    }

    [Fact]
    public async Task Kimliksiz_istek_surus_sahipligi_kontrolunden_gecemez()
    {
        using var saglayici = Kur();
        var yetki = saglayici.GetRequiredService<IAuthorizationService>();

        var sonuc = await yetki.AuthorizeAsync(
            Kullanici(kimlik: null, rol: RoleNames.Driver), Surus(SurucuA), PolicyNames.SurusSahibi);

        Assert.False(sonuc.Succeeded);
    }

    [Fact]
    public async Task Bos_guid_kimligi_surusun_bos_guid_surucusuyle_eslesmez()
    {
        using var saglayici = Kur();
        var yetki = saglayici.GetRequiredService<IAuthorizationService>();

        // Kimliği doğrulanmamış bir istekte CurrentUserAccessor Guid.Empty döner.
        // DriverId de bir hata sonucu Guid.Empty olsaydı, bu iki boşluk eşleşir
        // ve kimliksiz bir istek sahiplik kontrolünden geçerdi. Handler bunu
        // ayrıca eliyor.
        var sonuc = await yetki.AuthorizeAsync(
            Kullanici(Guid.Empty, RoleNames.Driver), Surus(Guid.Empty), PolicyNames.SurusSahibi);

        Assert.False(sonuc.Succeeded);
    }

    // --- Operatör bölgesi ---

    [Fact]
    public async Task Operator_kendi_bolgesindeki_goreve_erisebilir()
    {
        using var saglayici = Kur();
        var yetki = saglayici.GetRequiredService<IAuthorizationService>();

        var sonuc = await yetki.AuthorizeAsync(
            Kullanici(SurucuA, RoleNames.FieldOperator, bolge: "Seyhan"),
            new SahaGorevi("Seyhan"),
            PolicyNames.OperatorBolgesi);

        Assert.True(sonuc.Succeeded);
    }

    [Fact]
    public async Task Operator_baska_bolgedeki_goreve_erisemez()
    {
        using var saglayici = Kur();
        var yetki = saglayici.GetRequiredService<IAuthorizationService>();

        var sonuc = await yetki.AuthorizeAsync(
            Kullanici(SurucuA, RoleNames.FieldOperator, bolge: "Seyhan"),
            new SahaGorevi("Cukurova"),
            PolicyNames.OperatorBolgesi);

        Assert.False(sonuc.Succeeded);
    }

    [Fact]
    public async Task Bolgesiz_operator_hicbir_goreve_erisemez()
    {
        using var saglayici = Kur();
        var yetki = saglayici.GetRequiredService<IAuthorizationService>();

        var sonuc = await yetki.AuthorizeAsync(
            Kullanici(SurucuA, RoleNames.FieldOperator, bolge: null),
            new SahaGorevi("Seyhan"),
            PolicyNames.OperatorBolgesi);

        Assert.False(sonuc.Succeeded);
    }

    [Fact]
    public async Task Dogru_bolge_ama_yanlis_rol_gecemez()
    {
        using var saglayici = Kur();
        var yetki = saglayici.GetRequiredService<IAuthorizationService>();

        // Politika iki şart taşıyor: rol VE bölge. Biri sağlanıp diğeri
        // sağlanmadığında sonuç olumsuz olmalı.
        var sonuc = await yetki.AuthorizeAsync(
            Kullanici(SurucuA, RoleNames.Driver, bolge: "Seyhan"),
            new SahaGorevi("Seyhan"),
            PolicyNames.OperatorBolgesi);

        Assert.False(sonuc.Succeeded);
    }

    [Theory]
    [InlineData("Istanbul", "istanbul")]
    [InlineData("ISTANBUL", "Istanbul")]
    [InlineData("seyhan", "SEYHAN")]
    public async Task Bolge_karsilastirmasi_buyuk_kucuk_harften_etkilenmez(string kullaniciBolgesi, string gorevBolgesi)
    {
        using var saglayici = Kur();
        var yetki = saglayici.GetRequiredService<IAuthorizationService>();

        // Bu test Türkçe yerel ayarında ÇALIŞAN bir makinede de geçmeli.
        // Kültüre duyarlı karşılaştırma kullanılsaydı "I" harfi Türkçe'de
        // noktasız "i"ye dönüşür ve ilk iki satır kırmızı olurdu — yani
        // yetkilendirme kararı makinenin dil ayarına bağlı hale gelirdi.
        var sonuc = await yetki.AuthorizeAsync(
            Kullanici(SurucuA, RoleNames.FieldOperator, bolge: kullaniciBolgesi),
            new SahaGorevi(gorevBolgesi),
            PolicyNames.OperatorBolgesi);

        Assert.True(sonuc.Succeeded);
    }
}
