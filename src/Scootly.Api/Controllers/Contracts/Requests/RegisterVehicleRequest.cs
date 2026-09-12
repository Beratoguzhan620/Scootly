using System.ComponentModel.DataAnnotations;

namespace Scootly.Api.Contracts.Requests;

/// <summary>
/// Araç kayıt isteği.
/// </summary>
/// <remarks>
/// Sınırlar burada, veri açıklamalarıyla (data annotation) tanımlı. Alan modeli
/// de aynı kuralları ayrıca uyguluyor (<c>BatteryLevel</c>, <c>GeoPoint</c>
/// geçersiz değerde <c>DomainException</c> fırlatır). Bu tekrar bilinçli:
/// buradaki kontrol kullanıcıya 400 ve anlaşılır bir mesaj döndürmek için,
/// alan modelindeki kontrol ise API'yi hiç kullanmayan bir çağrı yolu
/// (test, arka plan işi, başka bir servis) açıldığında kuralın yine de
/// çiğnenememesi için.
/// </remarks>
public sealed record RegisterVehicleRequest(
    [property: Required, StringLength(64, MinimumLength = 2)] string Brand,
    [property: Range(1, 500)] int RangeKm,
    [property: Range(-90, 90)] double Latitude,
    [property: Range(-180, 180)] double Longitude,
    [property: Range(0, 100)] int BatteryPercentage);
