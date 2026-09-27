namespace Scootly.Application.IntegrationEvents;

/// <summary>Bir sürüş tamamlandı (63. gün).</summary>
/// <remarks>
/// Alanlar sade veri: kimlikler <see cref="Guid"/>, süre dakika, mesafe metre.
/// <c>RideId</c> gibi alan tipleri değil, çünkü tüketici <c>Scootly.Domain</c>'i
/// tanımak zorunda olmamalı.
/// </remarks>
public sealed record RideCompletedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid RideId,
    Guid DriverId,
    Guid VehicleId,
    double DurationMinutes,
    double DistanceMeters) : IIntegrationEvent, IHasEventName
{
    public static string EventName => "ride.completed";

    public void Validate()
    {
        if (EventId == Guid.Empty) throw new InvalidIntegrationEventException("EventId bos.");
        if (RideId == Guid.Empty) throw new InvalidIntegrationEventException("RideId bos.");
        if (DriverId == Guid.Empty) throw new InvalidIntegrationEventException("DriverId bos.");
        if (VehicleId == Guid.Empty) throw new InvalidIntegrationEventException("VehicleId bos.");
        if (DurationMinutes < 0) throw new InvalidIntegrationEventException("DurationMinutes negatif.");
        if (DistanceMeters < 0) throw new InvalidIntegrationEventException("DistanceMeters negatif.");
    }
}
