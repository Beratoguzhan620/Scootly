using Scootly.Domain.Common;
using Scootly.Domain.Fleet.Events;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding;

namespace Scootly.Domain.Fleet;

/// <summary>
/// Araç yaşam döngüsü:
/// Available → Reserved → InRide → Available;
/// Reserved → Available (iptal / süre dolumu);
/// InRide → Maintenance (terk edilmiş sürüş);
/// Available/Reserved/Lost → Maintenance → Available.
/// </summary>
public sealed class Vehicle : AggregateRoot
{
    public VehicleModel Model { get; }
    public VehicleStatus Status { get; private set; }
    public BatteryLevel Battery { get; private set; }
    public GeoPoint Location { get; private set; }
    public Guid? ReservedBy { get; private set; }
    public DateTime? ReservedAt { get; private set; }
    public DateTime? LastTelemetryAt { get; private set; }

    private Vehicle()
    {
        Model = null!;
        Battery = null!;
        Location = null!;
    }

    public Vehicle(VehicleId id, VehicleModel model, GeoPoint location, BatteryLevel battery, DateTime registeredAt)
        : base(id.Value)
    {
        Model = model;
        Location = location;
        Battery = battery;
        Status = VehicleStatus.Available;

        AddDomainEvent(new VehicleRegisteredEvent(id, registeredAt));
    }

    public void Reserve(Guid driverId, DateTime now)
    {
        if (driverId == Guid.Empty)
            throw new DomainException("Rezervasyon için sürücü kimliği gerekli.");

        EnsureStatusIs(VehicleStatus.Available, "Araç müsait değil, rezerve edilemez.");

        ReservedBy = driverId;
        ReservedAt = now;
        ChangeStatus(VehicleStatus.Reserved, now);
    }

    public void CancelReservation(Guid driverId, DateTime now)
    {
        EnsureStatusIs(VehicleStatus.Reserved, "Araç rezerve edilmemiş, rezervasyon iptal edilemez.");

        if (ReservedBy != driverId)
            throw new DomainException("Bu rezervasyon size ait değil.");

        ClearReservation();
        ChangeStatus(VehicleStatus.Available, now);
    }

    public void ExpireReservation(DateTime now)
    {
        EnsureStatusIs(VehicleStatus.Reserved, "Araç rezerve edilmemiş, rezervasyon süresi dolamaz.");

        if (ReservedAt is null || !ReservationPolicy.IsExpired(ReservedAt.Value, now))
            throw new DomainException("Rezervasyon süresi henüz dolmadı.");

        ClearReservation();
        ChangeStatus(VehicleStatus.Available, now);
    }

    public void StartRide(Guid driverId, DateTime now)
    {
        EnsureStatusIs(VehicleStatus.Reserved, "Araç rezerve edilmemiş, sürüş başlatılamaz.");

        if (ReservedBy != driverId)
            throw new DomainException("Araç başka bir sürücü tarafından rezerve edilmiş.");

        ClearReservation();
        ChangeStatus(VehicleStatus.InRide, now);
    }

    public void CompleteRide(GeoPoint parkedAt, DateTime now)
    {
        EnsureStatusIs(VehicleStatus.InRide, "Araç sürüşte değil, sürüş tamamlanamaz.");

        Location = parkedAt;
        ChangeStatus(VehicleStatus.Available, now);
    }

    /// <summary>Terk edilmiş bir sürüşten sonra araç, saha ekibi kontrol edene kadar bakıma alınır.</summary>
    public void EndAbandonedRide(DateTime now)
    {
        EnsureStatusIs(VehicleStatus.InRide, "Araç sürüşte değil, terk edilmiş sürüş kapatılamaz.");

        ChangeStatus(VehicleStatus.Maintenance, now);
    }

    public void SendToMaintenance(DateTime now)
    {
        switch (Status)
        {
            case VehicleStatus.InRide:
                throw new DomainException("Sürüşteki bir araç bakıma alınamaz; önce sürüş sonlanmalı.");
            case VehicleStatus.Maintenance:
                throw new DomainException("Araç zaten bakımda.");
        }

        ClearReservation();
        ChangeStatus(VehicleStatus.Maintenance, now);
    }

    public void ReturnToService(DateTime now)
    {
        if (Status is not (VehicleStatus.Maintenance or VehicleStatus.Lost))
            throw new DomainException("Yalnızca bakımdaki veya kayıp bir araç hizmete döndürülebilir.");

        ChangeStatus(VehicleStatus.Available, now);
    }

    /// <summary>
    /// Cihazdan gelen konum/batarya okumasını uygular. Sıra dışı (daha eski) okumalar yok sayılır.
    /// Batarya düşük eşiğin altına ilk kez indiğinde <see cref="VehicleBatteryLowEvent"/> üretilir.
    /// </summary>
    public bool ReportTelemetry(GeoPoint location, BatteryLevel battery, DateTime recordedAt)
    {
        if (LastTelemetryAt is not null && recordedAt <= LastTelemetryAt.Value)
            return false;

        var previousBattery = Battery;

        Location = location;
        Battery = battery;
        LastTelemetryAt = recordedAt;

        if (battery.HasDroppedBelowLowThresholdFrom(previousBattery))
            AddDomainEvent(new VehicleBatteryLowEvent(new VehicleId(Id), battery.Percentage, recordedAt));

        return true;
    }

    private void ClearReservation()
    {
        ReservedBy = null;
        ReservedAt = null;
    }

    private void EnsureStatusIs(VehicleStatus expected, string errorMessage)
    {
        if (Status != expected)
            throw new DomainException(errorMessage);
    }

    private void ChangeStatus(VehicleStatus newStatus, DateTime now)
    {
        var oldStatus = Status;
        Status = newStatus;

        AddDomainEvent(new VehicleStatusChangedEvent(
            new VehicleId(Id),
            oldStatus,
            newStatus,
            new GeoLocationSnapshot(Location.Latitude, Location.Longitude),
            now));
    }
}
