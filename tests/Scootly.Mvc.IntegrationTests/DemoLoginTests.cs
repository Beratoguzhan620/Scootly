using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Scootly.Mvc.IntegrationTests;

[Collection(MvcCollection.Name)]
public sealed class DemoLoginTests
{
    private readonly MvcFactory _factory;

    public DemoLoginTests(MvcFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Demo_Disinda_Giris_Sayfasi_Hesap_Bilgisi_Gostermemeli()
    {
        var page = await _factory.CreateBrowserClient().GetStringAsync("/Account/Login");

        Assert.DoesNotContain("herkese açık bir demo", page, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Demoda_Giris_Sayfasi_Yayimlanan_Filo_Yoneticisi_Girisini_Sunmali()
    {
        using var demo = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Demo:FleetManagerEmail", "yonetici@scootly.demo");
            builder.UseSetting("Demo:FleetManagerPassword", "Yonetici-Demo-2026");
        });
        var client = demo.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

        var page = await client.GetStringAsync("/Account/Login");

        Assert.Contains("herkese açık bir demo", page, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("yonetici@scootly.demo", page, StringComparison.Ordinal);
        Assert.Contains("Filo yöneticisi olarak gir", page, StringComparison.Ordinal);
    }
}
