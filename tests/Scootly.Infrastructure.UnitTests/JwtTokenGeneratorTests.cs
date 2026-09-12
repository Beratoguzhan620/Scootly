using Microsoft.IdentityModel.JsonWebTokens;
using Scootly.Infrastructure.Identity;

namespace Scootly.Infrastructure.UnitTests;

public sealed class JwtTokenGeneratorTests
{
    private const string TestImzaAnahtari =
        "bu-anahtar-yalnizca-testlerde-kullanilir-ve-32-bayttan-uzundur";

    private static readonly DateTime SabitZaman =
        new(2026, 9, 12, 10, 0, 0, DateTimeKind.Utc);

    private static JwtOptions Secenekler() => new()
    {
        Issuer = "scootly-test",
        Audience = "scootly-test-clients",
        SigningKey = TestImzaAnahtari,
        AccessTokenLifetimeMinutes = 60
    };

    private static ApplicationUser Kullanici(string? bolge = null) => new()
    {
        Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
        UserName = "surucu@scootly.local",
        Email = "surucu@scootly.local",
        HomeRegion = bolge
    };

    private static JsonWebToken Uret(ApplicationUser kullanici, params string[] roller)
    {
        var uretici = new JwtTokenGenerator(Secenekler(), new FakeClock(SabitZaman));
        return new JsonWebToken(uretici.GenerateToken(kullanici, roller));
    }

    [Fact]
    public void Token_kullanici_kimligini_sub_iddiasinda_tasir()
    {
        var kullanici = Kullanici();

        var token = Uret(kullanici, RoleNames.Driver);

        var sub = token.Claims.Single(iddia => iddia.Type == ScootlyClaimTypes.Subject);
        Assert.Equal(kullanici.Id.ToString(), sub.Value);
    }

    [Fact]
    public void Token_butun_rolleri_tasir()
    {
        var token = Uret(Kullanici(), RoleNames.Driver, RoleNames.FieldOperator);

        var roller = token.Claims
            .Where(iddia => iddia.Type == ScootlyClaimTypes.Role)
            .Select(iddia => iddia.Value)
            .OrderBy(deger => deger, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { RoleNames.Driver, RoleNames.FieldOperator }, roller);
    }

    [Fact]
    public void Token_eposta_veya_kullanici_adi_tasimaz()
    {
        // Token'ın gövdesi şifreli değil, yalnızca base64. İçine konan her şey
        // token'ı eline geçiren herkes tarafından okunabilir; bu yüzden
        // yetkilendirme için gerekmeyen hiçbir kişisel veri konmaz.
        var token = Uret(Kullanici(), RoleNames.Driver);

        Assert.DoesNotContain(
            token.Claims,
            iddia => iddia.Value.Contains("scootly.local", StringComparison.Ordinal));
    }

    [Fact]
    public void Bolge_bos_ise_iddia_hic_eklenmez()
    {
        var token = Uret(Kullanici(bolge: null), RoleNames.Driver);

        Assert.DoesNotContain(token.Claims, iddia => iddia.Type == ScootlyClaimTypes.HomeRegion);
    }

    [Fact]
    public void Bolge_dolu_ise_iddia_eklenir()
    {
        var token = Uret(Kullanici(bolge: "Seyhan"), RoleNames.FieldOperator);

        var bolge = token.Claims.Single(iddia => iddia.Type == ScootlyClaimTypes.HomeRegion);
        Assert.Equal("Seyhan", bolge.Value);
    }

    [Fact]
    public void Son_kullanma_saati_saate_gore_hesaplanir()
    {
        var token = Uret(Kullanici(), RoleNames.Driver);

        Assert.Equal(SabitZaman.AddMinutes(60), token.ValidTo);
    }

    [Fact]
    public void Ayni_kullanici_icin_uretilen_iki_token_ayni_degildir()
    {
        var kullanici = Kullanici();

        var birinci = Uret(kullanici, RoleNames.Driver);
        var ikinci = Uret(kullanici, RoleNames.Driver);

        // Her token'ın kendi jti değeri var. Aynı olsalardı, ileride bir token'ı
        // iptal etmek o kullanıcının bütün token'larını iptal etmek olurdu.
        Assert.NotEqual(
            birinci.Claims.Single(i => i.Type == ScootlyClaimTypes.TokenId).Value,
            ikinci.Claims.Single(i => i.Type == ScootlyClaimTypes.TokenId).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("kisa-anahtar")]
    [InlineData("otuz-iki-bayttan-kisa-anahtar")]
    public void Kisa_imza_anahtari_reddedilir(string anahtar)
    {
        var secenekler = Secenekler();
        secenekler.SigningKey = anahtar;

        // Zayıf anahtarla token üretmektense hiç üretmemek tercih edilir:
        // HMAC-SHA256'nın güvenliği anahtarın uzunluğuyla sınırlıdır.
        Assert.Throws<InvalidOperationException>(
            () => { _ = new JwtTokenGenerator(secenekler, new FakeClock(SabitZaman)); });
    }

    [Fact]
    public void Yayinci_ve_hedef_kitle_token_a_yazilir()
    {
        var token = Uret(Kullanici(), RoleNames.Driver);

        Assert.Equal("scootly-test", token.Issuer);
        Assert.Contains("scootly-test-clients", token.Audiences);
    }
}
