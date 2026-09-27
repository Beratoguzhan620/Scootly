using Scootly.Domain.Common;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding.Events;

namespace Scootly.Domain.Riding;

public sealed class Ride : AggregateRoot
{
    public Guid DriverId { get; }
    public Guid VehicleId { get; }
    public GeoPoint StartLocation { get; }
    public GeoPoint? EndLocation { get; private set; }
    public DateTime StartedAt { get; }
    public DateTime? EndedAt { get; private set; }
    public RideStatus Status { get; private set; }
    public decimal? Fare { get; private set; }

    /// <summary>Ödeme sonucu; NULL ise henüz sonuçlanmadı (69. gün).</summary>
    public RidePaymentStatus? PaymentStatus { get; private set; }

    private Ride()
    {
        StartLocation = null!;
    }

    public Ride(RideId id, Guid driverId, Guid vehicleId, GeoPoint startLocation, DateTime startedAt)
        : base(id.Value)
    {
        DriverId = driverId;
        VehicleId = vehicleId;
        StartLocation = startLocation;
        StartedAt = startedAt;
        Status = RideStatus.Active;

        AddDomainEvent(new RideStartedEvent(id, DateTime.UtcNow));
    }

    public void Complete(GeoPoint endLocation, DateTime endedAt)
    {
        if (Status != RideStatus.Active)
            throw new DomainException("Yalnızca aktif bir sürüş tamamlanabilir.");

        EndLocation = endLocation;
        EndedAt = endedAt;
        Status = RideStatus.Completed;

        var duration = endedAt - StartedAt;
        var distanceMeters = StartLocation.DistanceTo(endLocation);

        AddDomainEvent(new RideCompletedEvent(
            new RideId(Id), duration, distanceMeters, DateTime.UtcNow));
    }

    /// <summary>
    /// Hesaplanan ücreti sürüşe yazar (64. gün).
    /// </summary>
    /// <remarks>
    /// Ücret <see cref="Complete"/> içinde değil, sonradan yazılıyor: hesap
    /// artık sürüş bitirme isteğinin parçası değil, kuyruktan gelen bir
    /// olayın sonucu. İkinci kez çağrılması bir HATA — aynı sürüş için iki
    /// ücret yazılmaz. Tekrar teslim edilen mesajı sessizce atlamak
    /// tüketicinin işi; alan modeli yalnızca kuralı koruyor.
    /// </remarks>
    public void ApplyFare(decimal fare)
    {
        if (Status != RideStatus.Completed)
            throw new DomainException("Yalnızca tamamlanmış bir sürüşe ücret yazılabilir.");

        if (Fare is not null)
            throw new DomainException("Bu sürüşün ücreti zaten hesaplanmış.");

        if (fare < 0)
            throw new DomainException("Ücret negatif olamaz.");

        Fare = fare;
    }

    /// <summary>Ödeme alındı (69. gün — saga'nın başarılı sonu).</summary>
    public void MarkPaid()
    {
        EnsurePaymentCanBeConcluded();
        PaymentStatus = RidePaymentStatus.Paid;
    }

    /// <summary>
    /// Ödeme alınamadı (69. gün — telafi adımı).
    /// </summary>
    /// <remarks>
    /// Sürüş GERİ ALINMIYOR: kullanıcı sürdü, mesafe katedildi, bunu
    /// "olmamış" saymanın bir yolu yok. Telafi, olanı silmek değil, sonucu
    /// tutarlı hale getirmek — burada: sürüş "ödeme alınamadı" olarak
    /// işaretleniyor ve borç ayrı bir kayıtta açılıyor.
    /// </remarks>
    public void MarkPaymentFailed()
    {
        EnsurePaymentCanBeConcluded();
        PaymentStatus = RidePaymentStatus.PaymentFailed;
    }

    private void EnsurePaymentCanBeConcluded()
    {
        if (Status != RideStatus.Completed)
            throw new DomainException("Yalnızca tamamlanmış bir sürüşün ödemesi sonuçlanabilir.");

        if (Fare is null)
            throw new DomainException("Ücreti hesaplanmamış bir sürüşün ödemesi sonuçlanamaz.");

        if (PaymentStatus is not null)
            throw new DomainException("Bu sürüşün ödemesi zaten sonuçlanmış.");
    }

    public void Abandon()
    {
        if (Status != RideStatus.Active)
            throw new DomainException("Yalnızca aktif bir sürüş terk edilmiş sayılabilir.");

        Status = RideStatus.Abandoned;
        EndedAt = DateTime.UtcNow;

        AddDomainEvent(new RideAbandonedEvent(new RideId(Id), DateTime.UtcNow));
    }
}