using System;
using System.Collections.Generic;
using System.Text;
using Scootly.Domain.Common;
using Scootly.Domain.Geo;
using Xunit;

namespace Scootly.Domain.UnitTests;

public class GeoPointTests
{
    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    public void Gecersiz_Enlem_DomainException_Firlatmali(double lat, double lon)
    {
        Assert.Throws<DomainException>(() => new GeoPoint(lat, lon));
    }

    [Theory]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    public void Gecersiz_Boylam_DomainException_Firlatmali(double lat, double lon)
    {
        Assert.Throws<DomainException>(() => new GeoPoint(lat, lon));
    }

    [Fact]
    public void Bir_Derece_Enlem_Farki_Yaklasik_111_Km_Olmali()
    {
        var a = new GeoPoint(0, 0);
        var b = new GeoPoint(1, 0);

        var mesafe = a.DistanceTo(b);

        Assert.InRange(mesafe, 111_000, 111_400);
    }

    [Fact]
    public void Mesafe_Simetrik_Olmali()
    {
        var a = new GeoPoint(37.00, 35.32);
        var b = new GeoPoint(39.93, 32.85);

        Assert.Equal(a.DistanceTo(b), b.DistanceTo(a), precision: 5);
    }

    [Fact]
    public void Ayni_Koordinata_Sahip_Iki_Nokta_Esit_Olmali()
    {
        Assert.True(new GeoPoint(37.00, 35.32).Equals(new GeoPoint(37.00, 35.32)));
    }
}