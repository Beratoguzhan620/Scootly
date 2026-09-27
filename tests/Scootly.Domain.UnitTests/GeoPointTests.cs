using Scootly.Domain.Common;
using Scootly.Domain.Geo;
using Xunit;

namespace Scootly.Domain.UnitTests;

public class GeoPointTests
{
    [Fact]
    public void Gecersiz_Enlem_Ile_GeoPoint_Olusturulamaz()
    {
        Assert.Throws<DomainException>(() => new GeoPoint(91, 29));
    }

    [Fact]
    public void Gecersiz_Boylam_Ile_GeoPoint_Olusturulamaz()
    {
        Assert.Throws<DomainException>(() => new GeoPoint(41, 181));
    }

    [Theory]
    [InlineData(double.NaN, 29)]
    [InlineData(41, double.NaN)]
    [InlineData(double.PositiveInfinity, 29)]
    public void Sayi_Olmayan_Koordinat_Ile_GeoPoint_Olusturulamaz(double latitude, double longitude)
    {
        Assert.Throws<DomainException>(() => new GeoPoint(latitude, longitude));
    }

    [Fact]
    public void Ayni_Koordinatli_Iki_GeoPoint_Esit_Olmali()
    {
        var point1 = new GeoPoint(41.0, 29.0);
        var point2 = new GeoPoint(41.0, 29.0);

        Assert.Equal(point1, point2);
    }
}
