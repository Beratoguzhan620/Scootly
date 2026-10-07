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
/// Available/Reserved/Lost → Maintenance → Available;
/// Available/Reserved/Maintenance → Lost → Available (bulunduğunda).
/// </summary>
public sealed class Vehicle : AggregateRoot
{
    /// <summary>Cihazın bildirdiği konum bu süreden yeniyse, sürüş bitişinde istemcinin bildirdiği konuma tercih edilir.</summary>
    public static readonly TimeSpan TelemetryFreshness = TimeSpan.FromMinutes(2);

    public VehicleModel Model { get; private set; }
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

        if (!Battery.IsRentable)
            throw new DomainException($"Aracın bataryası kiralama için çok düşük (en az %{BatteryLevel.MinimumRentablePercentage}).");

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

    /// <summary>
    /// Sürüşü kapatır ve aracın park konumunu döndürür. Konumun yetkili kaynağı cihazdır: telemetri tazeyse
    /// araç son bildirdiği yerde kalır, istemcinin bildirdiği konum yalnızca cihaz susmuşsa kullanılır.
    /// </summary>
    public GeoPoint CompleteRide(GeoPoint reportedLocation, DateTime now)
    {
        EnsureStatusIs(VehicleStatus.InRide, "Araç sürüşte değil, sürüş tamamlanamaz.");

        if (!HasFreshTelemetry(now))
            Location = reportedLocation;

        ChangeStatus(VehicleStatus.Available, now);

        return Location;
    }

    public bool HasFreshTelemetry(DateTime now)
        => LastTelemetryAt is { } lastTelemetryAt && now - lastTelemetryAt <= TelemetryFreshness;

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

    /// <summary>Bulunamayan (sinyal vermeyen, çalınmış) araç kiralamadan çekilir; bulununca hizmete döndürülür.</summary>
    public void MarkLost(DateTime now)
    {
        switch (Status)
        {
            case VehicleStatus.InRide:
                throw new DomainException("Sürüşteki bir araç kayıp olarak işaretlenemez; önce sürüş sonlanmalı.");
            case VehicleStatus.Lost:
                throw new DomainException("Araç zaten kayıp olarak işaretli.");
        }

        ClearReservation();
        ChangeStatus(VehicleStatus.Lost, now);
    }

    public void ReturnToService(DateTime now)
    {
        if (Status is not (VehicleStatus.Maintenance or VehicleStatus.Lost))
            throw new DomainException("Yalnızca bakımdaki veya kayıp bir araç hizmete döndürülebilir.");

        ChangeStatus(VehicleStatus.Available, now);
    }

    /// <summary>Marka/menzil bilgisini günceller. Sürüşteki bir araç düzenlenemez.</summary>
    public void UpdateModel(VehicleModel model)
    {
        if (Status == VehicleStatus.InRide)
            throw new DomainException("Sürüşteki bir araç düzenlenemez.");

        Model = model;
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
