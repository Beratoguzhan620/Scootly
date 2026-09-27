using System.Globalization;
using System.Text;
using Scootly.Infrastructure.Payments;
using Xunit;

namespace Scootly.Infrastructure.UnitTests;

/// <summary>70. gun — imzasiz ya da degistirilmis webhook kabul edilmemeli.</summary>
public sealed class PaymentWebhookValidatorTests
{
    private const string Sir = "test-webhook-sirri-en-az-32-karakter-uzunlugunda";
    private static readonly DateTimeOffset Simdi = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Govde = Encoding.UTF8.GetBytes("""{"eventId":"11111111-1111-1111-1111-111111111111","success":true}""");

    private static PaymentWebhookValidator Dogrulayici(string sir = Sir, int tolerans = 300)
        => new(new PaymentOptions { WebhookSecret = sir, WebhookToleranceSeconds = tolerans }, new SabitZaman(Simdi));

    private static string Damga(DateTimeOffset t) => t.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

    [Fact]
    public void Dogru_Imza_Kabul_Edilir()
    {
        var damga = Damga(Simdi);
        var imza = PaymentWebhookValidator.ComputeHex(Sir, damga, Govde);

        Assert.True(Dogrulayici().IsValid(damga, imza, Govde));
    }

    [Fact]
    public void Govdede_Tek_Bayt_Degisirse_Reddedilir()
    {
        var damga = Damga(Simdi);
        var imza = PaymentWebhookValidator.ComputeHex(Sir, damga, Govde);
        var degismis = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Govde).Replace("true", "fals", StringComparison.Ordinal) + "e");

        Assert.False(Dogrulayici().IsValid(damga, imza, degismis));
    }

    [Fact]
    public void Baska_Sirla_Imzalanan_Istek_Reddedilir()
    {
        var damga = Damga(Simdi);
        var imza = PaymentWebhookValidator.ComputeHex("baska-bir-sir-ile-imzalanmis-sahte-istek", damga, Govde);

        Assert.False(Dogrulayici().IsValid(damga, imza, Govde));
    }

    [Fact]
    public void Eski_Istek_Tekrar_Oynatilamaz()
    {
        // Gecerli ama 10 dakika onceki bir istek: yakalanip tekrar gonderilmis.
        var eski = Damga(Simdi.AddMinutes(-10));
        var imza = PaymentWebhookValidator.ComputeHex(Sir, eski, Govde);

        Assert.False(Dogrulayici(tolerans: 300).IsValid(eski, imza, Govde));
    }

    [Fact]
    public void Damga_Degistirilirse_Imza_Tutmaz()
    {
        // Saldirgan eski istegin damgasini guncelleyemez: damga imzanin icinde.
        var eski = Damga(Simdi.AddMinutes(-10));
        var imza = PaymentWebhookValidator.ComputeHex(Sir, eski, Govde);

        Assert.False(Dogrulayici().IsValid(Damga(Simdi), imza, Govde));
    }

    [Theory]
    [InlineData(null, "ab")]
    [InlineData("", "ab")]
    [InlineData("123", null)]
    [InlineData("damga-degil", "ab")]
    [InlineData("1790510400", "onaltilik-degil")]
    public void Eksik_Ya_Da_Bozuk_Baslik_Reddedilir(string? damga, string? imza)
    {
        Assert.False(Dogrulayici().IsValid(damga, imza, Govde));
    }

    [Fact]
    public void Sir_Tanimli_Degilse_Her_Istek_Reddedilir()
    {
        // Kapali basarisizlik: yapilandirma eksikse kapi acik degil kapali kalir.
        var damga = Damga(Simdi);
        var imza = PaymentWebhookValidator.ComputeHex(Sir, damga, Govde);
        var v = Dogrulayici(sir: "");

        Assert.False(v.IsConfigured);
        Assert.False(v.IsValid(damga, imza, Govde));
    }

    private sealed class SabitZaman : TimeProvider
    {
        private readonly DateTimeOffset _an;
        public SabitZaman(DateTimeOffset an) => _an = an;
        public override DateTimeOffset GetUtcNow() => _an;
    }
}
