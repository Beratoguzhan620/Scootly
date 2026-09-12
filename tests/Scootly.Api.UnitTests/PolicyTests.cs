using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Scootly.Api.Authorization;
using Scootly.Infrastructure.Identity;

namespace Scootly.Api.UnitTests;

/// <summary>
/// Politikaların gerçekten doğru kararı verdiğini sınar.
/// Web sunucusu, veritabanı ve Docker gerektirmez — yetkilendirme motoru
/// tek başına kurulup sorgulanıyor.
/// </summary>
public sealed class PolicyTests
{
    private static ServiceProvider Kur()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScootlyAuthorization();
        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal Kullanici(string? rol = null, string? bolge = null)
    {
        var iddialar = new List<Claim>
        {
            new(ScootlyClaimTypes.Subject, Guid.NewGuid().ToString())
        };

        if (rol is not null)
        {
            iddialar.Add(new Claim(ScootlyClaimTypes.Role, rol));
        }

        if (bolge is not null)
        {
            iddialar.Add(new Claim(ScootlyClaimTypes.HomeRegion, bolge));
        }

        // nameType ve roleType, Program.cs'teki TokenValidationParameters ile
        // aynı olmak zorunda. Farklı olsalardı bu testler geçer ama gerçek
        // istekler 403 dönerdi — testin gerçeği yansıtması buna bağlı.
        var kimlik = new ClaimsIdentity(
            iddialar,
            authenticationType: "Test",
            nameType: ScootlyClaimTypes.Subject,
            roleType: ScootlyClaimTypes.Role);

        return new ClaimsPrincipal(kimlik);
    }

    [Theory]
    // Doğru rol geçer
    [InlineData(RoleNames.FleetManager, PolicyNames.SadeceYonetici, true)]
    [InlineData(RoleNames.FieldOperator, PolicyNames.SadeceOperator, true)]
    [InlineData(RoleNames.Driver, PolicyNames.SadeceSurucu, true)]
    [InlineData(RoleNames.Auditor, PolicyNames.SadeceDenetci, true)]
    // Yanlış rol geçmez — asıl önemli olan bu yön
    [InlineData(RoleNames.Driver, PolicyNames.SadeceYonetici, false)]
    [InlineData(RoleNames.FieldOperator, PolicyNames.SadeceYonetici, false)]
    [InlineData(RoleNames.Auditor, PolicyNames.SadeceYonetici, false)]
    [InlineData(RoleNames.Driver, PolicyNames.SadeceOperator, false)]
    [InlineData(RoleNames.FleetManager, PolicyNames.SadeceSurucu, false)]
    public async Task Rol_politikalari_yalnizca_dogru_role_izin_verir(
        string rol, string politika, bool beklenen)
    {
        using var saglayici = Kur();
        var yetki = saglayici.GetRequiredService<IAuthorizationService>();

        var sonuc = await yetki.AuthorizeAsync(Kullanici(rol), null, politika);

        Assert.Equal(beklenen, sonuc.Succeeded);
    }

    [Fact]
    public async Task Rolsuz_kullanici_hicbir_rol_politikasindan_gecemez()
    {
        using var saglayici = Kur();
        var yetki = saglayici.GetRequiredService<IAuthorizationService>();
        var rolsuz = Kullanici(rol: null);

        foreach (var politika in new[]
                 {
                     PolicyNames.SadeceYonetici,
                     PolicyNames.SadeceOperator,
                     PolicyNames.SadeceSurucu,
                     PolicyNames.SadeceDenetci
                 })
        {
            var sonuc = await yetki.AuthorizeAsync(rolsuz, null, politika);
            Assert.False(sonuc.Succeeded);
        }
    }

    [Fact]
    public async Task Bolge_iddiasi_olan_kullanici_iddia_politikasindan_gecer()
    {
        using var saglayici = Kur();
        var yetki = saglayici.GetRequiredService<IAuthorizationService>();

        var sonuc = await yetki.AuthorizeAsync(
            Kullanici(RoleNames.FieldOperator, bolge: "Seyhan"),
            null,
            PolicyNames.BolgeliPersonel);

        Assert.True(sonuc.Succeeded);
    }

    [Fact]
    public async Task Bolge_iddiasi_olmayan_kullanici_iddia_politikasindan_gecemez()
    {
        using var saglayici = Kur();
        var yetki = saglayici.GetRequiredService<IAuthorizationService>();

        // Rolü doğru ama bölgesi yok: rol tabanlı ile iddia tabanlı yetkinin
        // farklı sorular sorduğunun kanıtı.
        var sonuc = await yetki.AuthorizeAsync(
            Kullanici(RoleNames.FieldOperator, bolge: null),
            null,
            PolicyNames.BolgeliPersonel);

        Assert.False(sonuc.Succeeded);
    }

    [Fact]
    public async Task Varsayilan_politika_kimlik_dogrulamasi_zorunlu_kilar()
    {
        using var saglayici = Kur();
        var saglayiciPolitika = saglayici.GetRequiredService<IAuthorizationPolicyProvider>();

        var varsayilan = await saglayiciPolitika.GetFallbackPolicyAsync();

        // Bu test, "yeni eklenen uç varsayılan olarak kapalıdır" kuralının
        // yanlışlıkla kaldırılmasını yakalar.
        Assert.NotNull(varsayilan);
        Assert.Contains(varsayilan.Requirements, sart => sart is DenyAnonymousAuthorizationRequirement);
    }
}
