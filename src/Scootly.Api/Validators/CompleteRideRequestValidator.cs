using Scootly.Api.Contracts.Requests;

namespace Scootly.Api.Validators;

/// <summary>
/// <c>POST /rides/{id}/complete</c> gövdesini doğrular.
/// </summary>
/// <remarks>
/// Koordinat aralığı kontrolü sınır (boundary) kontrolüdür, iş kuralı değil:
/// 91 derece enlem diye bir yer yok. Bunu uçta reddetmek, alan modeline hiç
/// oluşamayacak bir GeoPoint'in ulaşmasını engelliyor.
/// </remarks>
public sealed class CompleteRideRequestValidator
{
    public (bool IsValid, string? Error) Validate(CompleteRideRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.EndLatitude is < -90 or > 90)
        {
            return (false, "EndLatitude -90 ile 90 arasında olmalı.");
        }

        if (request.EndLongitude is < -180 or > 180)
        {
            return (false, "EndLongitude -180 ile 180 arasında olmalı.");
        }

        // double.NaN hiçbir aralık karşılaştırmasını geçmez; yukarıdaki iki
        // kontrol NaN'ı zaten yakalar. Ayrı bir kontrol eklemek gereksiz olurdu.
        return (true, null);
    }
}
