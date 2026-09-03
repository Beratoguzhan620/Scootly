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

    [Fact]
    public void Ayni_Koordinatli_Iki_GeoPoint_Esit_Olmali()
    {
        var point1 = new GeoPoint(41.0, 29.0);
        var point2 = new GeoPoint(41.0, 29.0);

        Assert.Equal(point1, point2);
    }
}