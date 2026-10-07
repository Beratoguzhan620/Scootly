using Scootly.Domain.Common;

namespace Scootly.Domain.Fleet.Events;

public sealed record VehicleStatusChangedEvent(
    VehicleId VehicleId,
    VehicleStatus OldStatus,
    VehicleStatus NewStatus,
    GeoLocationSnapshot Location,
    DateTime OccurredOn) : IDomainEvent;

/// <summary>Olay anındaki konumun değiştirilemez kopyası (olay tüketicileri bölge hesabı için kullanır).</summary>
public sealed record GeoLocationSnapshot(double Latitude, double Longitude);
