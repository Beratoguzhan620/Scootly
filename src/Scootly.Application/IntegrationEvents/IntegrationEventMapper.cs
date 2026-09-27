using Scootly.Domain.Riding;
using Scootly.Domain.Riding.Events;

namespace Scootly.Application.IntegrationEvents;

/// <summary>
/// Domain olayını entegrasyon olayına çevirir (63. gün).
/// </summary>
/// <remarks>
/// <para>
/// Çeviri tek yerde. <c>RideCompletedEvent</c> sürücü ve araç kimliğini
/// taşımıyor — aggregate'in içinde zaten bilindikleri için gerek yoktu. Dışarı
/// giden olayda ise gerekliler, bu yüzden çeviri aggregate'i de alıyor.
/// </para>
/// <para>
/// Domain olayına bu alanları eklemek de bir seçenekti; reddedildi, çünkü o
/// zaman domain olayı dış dünyanın ihtiyacına göre şekillenmeye başlardı ki bu
/// ayrımın var olma sebebi tam olarak bunu önlemek.
/// </para>
/// </remarks>
public static class IntegrationEventMapper
{
    public static RideCompletedIntegrationEvent ToIntegrationEvent(this RideCompletedEvent domainEvent, Ride ride)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ArgumentNullException.ThrowIfNull(ride);

        if (domainEvent.RideId.Value != ride.Id)
            throw new ArgumentException("Olay bu surushe ait degil.", nameof(ride));

        return new RideCompletedIntegrationEvent(
            EventId: Guid.NewGuid(),
            OccurredOnUtc: domainEvent.OccurredOn,
            RideId: ride.Id,
            DriverId: ride.DriverId,
            VehicleId: ride.VehicleId,
            DurationMinutes: Math.Round(domainEvent.Duration.TotalMinutes, 2),
            DistanceMeters: Math.Round(domainEvent.DistanceMeters, 1));
    }
}
