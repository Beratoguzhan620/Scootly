using Scootly.Domain.Common;
using Scootly.Domain.Fleet.Events;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding;

namespace Scootly.Domain.Fleet;

public sealed class Vehicle : AggregateRoot
{
    public VehicleModel Model { get; }
    public VehicleStatus Status { get; private set; }
    public BatteryLevel Battery { get; private set; }
    public GeoPoint Location { get; private set; }

    /// <summary>
    /// Rezervasyonun ne zaman kendiliğinden düşeceği. Rezerve değilken <c>null</c>.
    /// </summary>
    /// <remarks>
    /// 54. günün rezervasyon zaman aşımı servisi bu alanı tarıyor. Alan
    /// modelinde olmasının sebebi, "rezervasyon süresi doldu mu" sorusunun bir
    /// iş kuralı olması — arka plan servisinin kendi içinde tuttuğu bir süre
    /// olsaydı, aynı kuralı API tarafında da yeniden yazmak gerekirdi ve iki
    /// kopya er geç ayrışırdı.
    /// </remarks>
    public DateTime? ReservedUntil { get; private set; }

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

    /// <summary>Aracı rezerve eder.</summary>
    /// <param name="reservedAt">
    /// Rezervasyon anı. Verilmezse sistem saati kullanılır — mevcut çağrı
    /// yerlerini kırmamak için isteğe bağlı, ama saati dışarıdan vermek
    /// (<c>IClock</c> ile) test edilebilirliği artırıyor.
    /// </param>
    public void Reserve(DateTime? reservedAt = null)
    {
        EnsureStatusIs(VehicleStatus.Available, "Araç müsait değil, rezerve edilemez.");

        ReservedUntil = (reservedAt ?? DateTime.UtcNow)
            .AddMinutes(ReservationPolicy.ReservationDurationMinutes);

        ChangeStatus(VehicleStatus.Reserved);
    }

    /// <summary>
    /// Süresi dolan rezervasyonu düşürür (54. gün).
    /// </summary>
    /// <remarks>
    /// Durumu doğrudan <c>Available</c>'a çekmek yerine ayrı bir metot olması,
    /// arka plan servisinin alan kuralını kendi içinde yeniden yazmasını
    /// engelliyor: "yalnızca rezerve bir araç serbest bırakılabilir" kuralı
    /// burada, tek yerde.
    /// </remarks>
    public void ReleaseReservation()
    {
        EnsureStatusIs(VehicleStatus.Reserved, "Araç rezerve değil, rezervasyon düşürülemez.");

        ReservedUntil = null;
        ChangeStatus(VehicleStatus.Available);
    }

    public void StartRide()
    {
        EnsureStatusIs(VehicleStatus.Reserved, "Araç rezerve edilmemiş, sürüş başlatılamaz.");

        // Sürüş başladıktan sonra rezervasyon süresi anlamsız. Temizlenmeseydi
        // zaman aşımı servisi sürüşteki bir aracı "süresi dolmuş rezervasyon"
        // sanıp serbest bırakmaya çalışırdı.
        ReservedUntil = null;

        ChangeStatus(VehicleStatus.InRide);
    }

    public void CompleteRide()
    {
        EnsureStatusIs(VehicleStatus.InRide, "Araç sürüşte değil, sürüş tamamlanamaz.");
        ChangeStatus(VehicleStatus.Available);
    }

    public void SendToMaintenance()
    {
        ChangeStatus(VehicleStatus.Maintenance);
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