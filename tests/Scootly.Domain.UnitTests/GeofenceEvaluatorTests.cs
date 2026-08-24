using Scootly.Domain.Geo;
using Xunit;

namespace Scootly.Domain.UnitTests;

public class GeofenceEvaluatorTests
{
    private static ServiceArea CreateSquareArea()
    {
        // Basit bir kare bölge: (0,0), (0,10), (10,10), (10,0)
        var boundary = new List<GeoPoint>
        {
            new(0, 0),
            new(0, 10),
            new(10, 10),
            new(10, 0)
        };

        return new ServiceArea("Test Bölgesi", boundary);
    }

    [Fact]
    public void Karenin_Icindeki_Nokta_Icerde_Sayilmali()
    {
        var area = CreateSquareArea();
        var evaluator = new GeofenceEvaluator();
        var pointInside = new GeoPoint(5, 5);

        var result = evaluator.IsInsideArea(pointInside, area);

        Assert.True(result);
    }

    [Fact]
    public void Karenin_Disindaki_Nokta_Disarda_Sayilmali()
    {
        var area = CreateSquareArea();
        var evaluator = new GeofenceEvaluator();
        var pointOutside = new GeoPoint(50, 50);

        var result = evaluator.IsInsideArea(pointOutside, area);

        Assert.False(result);
    }

    [Fact]
    public void GeoPoint_DistanceTo_Kendine_Sifir_Donmeli()
    {
        var point = new GeoPoint(41.0, 29.0);

        var distance = point.DistanceTo(point);

        Assert.Equal(0, distance, precision: 5);
    }
}