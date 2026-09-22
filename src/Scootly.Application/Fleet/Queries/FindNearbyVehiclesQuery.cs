namespace Scootly.Application.Fleet.Queries;

/// <summary>
/// "Şu noktanın yakınındaki müsait araçlar" sorgusu.
/// </summary>
/// <remarks>
/// <para>
/// 42. güne kadar bu uç adı "yakındaki araçlar" olmasına rağmen hiçbir konum
/// filtresi içermiyordu: tablodaki her aracı sayfalayarak döndürüyordu. Ad ile
/// davranış arasındaki bu fark, veri azken kimsenin fark etmediği türden bir
/// hata — elli araçla "çalışıyor" görünür, beş bin araçla harita ekranı
/// kullanılamaz hale gelir.
/// </para>
/// <para>
/// <see cref="RadiusMeters"/> için üst sınır var. Sınırsız bir yarıçap,
/// kimlik doğrulaması gerektirmeyen bir uçta "bütün tabloyu getir"in başka bir
/// yazımı olurdu — <c>PageRequest.MaxPageSize</c> ile aynı gerekçe.
/// </para>
/// </remarks>
public sealed record FindNearbyVehiclesQuery(
    double Latitude,
    double Longitude,
    double RadiusMeters,
    int PageNumber,
    int PageSize)
{
    public const double MaxRadiusMeters = 5_000d;
    public const double DefaultRadiusMeters = 1_000d;
}
