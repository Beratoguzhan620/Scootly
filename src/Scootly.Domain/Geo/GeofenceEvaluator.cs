namespace Scootly.Domain.Geo;

public sealed class GeofenceEvaluator
{
    public bool IsInsideArea(GeoPoint point, ServiceArea area)
    {
        return IsPointInPolygon(point, area.Boundary);
    }

    public bool IsInNoParkingZone(GeoPoint point, NoParkingZone zone)
    {
        return IsPointInPolygon(point, zone.Boundary);
    }

    private static bool IsPointInPolygon(GeoPoint point, IReadOnlyList<GeoPoint> polygon)
    {
        var isInside = false;
        var j = polygon.Count - 1;

        for (var i = 0; i < polygon.Count; i++)
        {
            var vertexI = polygon[i];
            var vertexJ = polygon[j];

            var intersects =
                (vertexI.Latitude > point.Latitude) != (vertexJ.Latitude > point.Latitude) &&
                point.Longitude < (vertexJ.Longitude - vertexI.Longitude) *
                    (point.Latitude - vertexI.Latitude) /
                    (vertexJ.Latitude - vertexI.Latitude) + vertexI.Longitude;

            if (intersects)
                isInside = !isInside;

            j = i;
        }

        return isInside;
    }
}