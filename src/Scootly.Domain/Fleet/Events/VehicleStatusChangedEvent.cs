using Scootly.Domain.Common;

namespace Scootly.Domain.Fleet.Events;

public sealed class VehicleStatusChangedEvent : IDomainEvent
{
    public VehicleId VehicleId { get; }
    public VehicleStatus OldStatus { get; }
    public VehicleStatus NewStatus { get; }
    public GeoLocationSnapshot Location { get; }
    public DateTime OccurredOn { get; }

    public VehicleStatusChangedEvent(
        VehicleId vehicleId, VehicleStatus oldStatus, VehicleStatus newStatus, GeoLocationSnapshot location, DateTime occurredOn)
    {
        VehicleId = vehicleId;
        OldStatus = oldStatus;
        NewStatus = newStatus;
        Location = location;
        OccurredOn = occurredOn;
    }
}

/// <summary>Olay anındaki konumun değiştirilemez kopyası (olay tüketicileri bölge hesabı için kullanır).</summary>
public sealed record GeoLocationSnapshot(double Latitude, double Longitude);
