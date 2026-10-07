namespace Scootly.Infrastructure.Caching;

public static class CacheKeys
{
    /// <summary>Anonim varsayılan araç listesi (yalnızca müsait araçlar, ilk sayfa).</summary>
    public static string NearbyVehiclesDefault() => "vehicles:public:default";
}
