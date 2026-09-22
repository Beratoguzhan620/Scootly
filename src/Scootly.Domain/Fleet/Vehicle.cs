using Scootly.Domain.Common;
using Scootly.Domain.Fleet.Events;
using Scootly.Domain.Geo;

namespace Scootly.Domain.Fleet;

public sealed class Vehicle : AggregateRoot
{
    public VehicleModel Model { get; }
    public VehicleStatus Status { get; private set; }
    public BatteryLevel Battery { get; private set; }
    public GeoPoint Location { get; private set; }
    public DateTime? ReservedAt { get; private set; }

    private Vehicle()
    {
        Model = null!;
        Battery = null!;
        Location = null!;
    }

    public Vehicle(VehicleId id, VehicleModel model, GeoPoint location, BatteryLevel battery)
        : base(id.Value)
    {
        Model = model;
        Location = location;
        Battery = battery;
        Status = VehicleStatus.Available;

        AddDomainEvent(new VehicleRegisteredEvent(id, DateTime.UtcNow));
    }

    public void Reserve()
    {
        EnsureStatusIs(VehicleStatus.Available, "Araç müsait değil, rezerve edilemez.");
        ChangeStatus(VehicleStatus.Reserved);
        ReservedAt = DateTime.UtcNow;
    }

    public void CancelReservation()
    {
        EnsureStatusIs(VehicleStatus.Reserved, "Araç rezerve edilmemiş, rezervasyon iptal edilemez.");
        ChangeStatus(VehicleStatus.Available);
        ReservedAt = null;
    }

    public void StartRide()
    {
        EnsureStatusIs(VehicleStatus.Reserved, "Araç rezerve edilmemiş, sürüş başlatılamaz.");
        ChangeStatus(VehicleStatus.InRide);
        ReservedAt = null;
    }

    public void CompleteRide()
    {
        EnsureStatusIs(VehicleStatus.InRide, "Araç sürüşte değil, sürüş tamamlanamaz.");
        ChangeStatus(VehicleStatus.Available);
    }

    public void SendToMaintenance()
    {
        ChangeStatus(VehicleStatus.Maintenance);
        ReservedAt = null;
    }

    private void EnsureStatusIs(VehicleStatus expected, string errorMessage)
    {
        if (Status != expected)
            throw new DomainException(errorMessage);
    }

    private void ChangeStatus(VehicleStatus newStatus)
    {
        var oldStatus = Status;
        Status = newStatus;

        AddDomainEvent(new VehicleStatusChangedEvent(
            new VehicleId(Id), oldStatus, newStatus, DateTime.UtcNow));
    }
}