using Scootly.Domain.Common;
using Scootly.Domain.Pricing;
using Xunit;

namespace Scootly.Domain.UnitTests;

public class TariffTests
{
    private static readonly Tariff Tarife = new(UnlockFee: 10m, PerMinuteRate: 2.5m);

    [Theory]
    [InlineData(0, 10.0)]        // hic surmediyse yalnizca acilis
    [InlineData(60, 12.5)]       // tam bir dakika
    [InlineData(61, 15.0)]       // bir dakika bir saniye = iki baslayan dakika
    [InlineData(600, 35.0)]      // on dakika
    public void Ucret_Baslayan_Her_Dakika_Icin_Alinir(int saniye, double beklenen)
    {
        var ucret = Tarife.Calculate(TimeSpan.FromSeconds(saniye));

        Assert.Equal((decimal)beklenen, ucret);
    }

    [Fact]
    public void Negatif_Sure_Reddedilir()
    {
        Assert.Throws<DomainException>(() => Tarife.Calculate(TimeSpan.FromSeconds(-1)));
    }
}
