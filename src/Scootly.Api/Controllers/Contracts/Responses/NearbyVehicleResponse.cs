namespace Scootly.Api.Contracts.Responses;

/// <summary>Harita ekranının bir araç için gördüğü alanlar.</summary>
/// <remarks>
/// <para>
/// <c>Status</c> YOK, çünkü bu uç yalnızca müsait araçları döndürüyor — her
/// satırda aynı değeri taşımak, istemciye filtrenin var olmadığını düşündüren
/// bir alan eklemek olurdu.
/// </para>
/// <para>
/// Bu, <see cref="VehicleResponse"/>'un neredeyse aynısı ve öyle kalması
/// gerekiyor: biri harita ucunun sözleşmesi, diğeri genel araç listesininki.
/// Tek tip paylaşsalardı, haritaya bir alan eklemek yönetim listesini de
/// değiştirirdi.
/// </para>
/// </remarks>
public sealed record NearbyVehicleResponse(
    Guid Id,
    double Latitude,
    double Longitude,
    int BatteryPercentage);
