// Nullable analizi kapalı: Serilog'un ILogEventPropertyValueFactory arayüzündeki
// işaretlemeler sürümden sürüme değişiyor ve bu projede uyarılar hata sayılıyor.
#nullable disable

using Scootly.Api.Logging;
using Serilog.Core;
using Serilog.Events;

namespace Scootly.Api.UnitTests;

public sealed class SensitiveDataMaskingTests
{
    private sealed class SahteFabrika : ILogEventPropertyValueFactory
    {
        public LogEventPropertyValue CreatePropertyValue(object value, bool destructureObjects = false)
            => new ScalarValue(value);
    }

    // Namespace'i "Scootly" ile başlayan bir tip olmalı; kural yalnızca kendi
    // tiplerimize karışıyor.
    private sealed class SahteIstek
    {
        public string Email { get; init; } = "surucu@scootly.local";
        public string Password { get; init; } = "gercek-parola-2026";
        public string ConfirmPassword { get; init; } = "gercek-parola-2026";
        public string AccessToken { get; init; } = "eyJhbGciOi-uzun-token";
        public string DeviceSecret { get; init; } = "cihaz-sirri-12345";
        public int VehicleCount { get; init; } = 7;
    }

    private static IReadOnlyDictionary<string, string> Maskele()
    {
        var kural = new SensitiveDataDestructuringPolicy();

        var basarili = kural.TryDestructure(new SahteIstek(), new SahteFabrika(), out var sonuc);
        Assert.True(basarili);

        var yapi = Assert.IsType<StructureValue>(sonuc);

        return yapi.Properties.ToDictionary(
            p => p.Name,
            p => ((ScalarValue)p.Value).Value?.ToString() ?? string.Empty,
            StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("Password")]
    [InlineData("ConfirmPassword")]
    [InlineData("AccessToken")]
    [InlineData("DeviceSecret")]
    public void Hassas_alanlar_maskelenir(string alanAdi)
    {
        var alanlar = Maskele();

        Assert.Equal("***", alanlar[alanAdi]);
    }

    [Fact]
    public void Hassas_olmayan_alanlar_oldugu_gibi_kalir()
    {
        var alanlar = Maskele();

        Assert.Equal("surucu@scootly.local", alanlar["Email"]);
        Assert.Equal("7", alanlar["VehicleCount"]);
    }

    [Fact]
    public void Gercek_degerlerin_hicbiri_ciktida_yer_almaz()
    {
        var alanlar = Maskele();
        var hepsi = string.Join("|", alanlar.Values);

        // Asıl iddia bu: parolanın, token'ın ve cihaz sırrının hiçbir izi
        // loga giden yapının içinde kalmamalı.
        Assert.DoesNotContain("gercek-parola-2026", hepsi, StringComparison.Ordinal);
        Assert.DoesNotContain("eyJhbGciOi-uzun-token", hepsi, StringComparison.Ordinal);
        Assert.DoesNotContain("cihaz-sirri-12345", hepsi, StringComparison.Ordinal);
    }

    [Fact]
    public void Kutuphane_tipleri_degistirilmez()
    {
        var kural = new SensitiveDataDestructuringPolicy();

        // Kendi namespace'imiz dışındaki tiplere karışmıyoruz: yansımayla
        // gezmek pahalı ve bazı özellikler okunduğunda yan etki üretiyor.
        var basarili = kural.TryDestructure(new Uri("https://ornek.test/"), new SahteFabrika(), out _);

        Assert.False(basarili);
    }
}
