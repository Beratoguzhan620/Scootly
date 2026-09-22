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

    /// <summary>
    /// Terk edilmiş sürüşü kapatır (54. gün).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Complete"/>'ten ayrı bir metot, çünkü ayrı bir olay: burada
    /// bitiş konumu YOK. Kullanıcı sürüşü bitirmedi; sistem uzun süre hareket
    /// görmediği için kapatıyor. <c>Complete</c>'e sahte bir bitiş konumu
    /// uydurup göndermek, veriye "bu sürüş normal bitti" diye yalan söylemek
    /// olurdu — ve o yalan, mesafe/ücret raporlarında ortaya çıkardı.
    /// </para>
    /// <para>
    /// Ücret hesaplanmıyor. Terk edilmiş bir sürüşün ücretlendirilmesi bir
    /// iş kararı (ve muhtemelen bir müşteri şikayeti); alan modeli o kararı
    /// kendi başına vermiyor.
    /// </para>
    /// </remarks>
    public void MarkAbandoned(DateTime detectedAt)
    {
        if (Status != RideStatus.Active)
        {
            throw new DomainException("Yalnızca aktif bir sürüş terk edilmiş sayılabilir.");
        }

        EndedAt = detectedAt;
        Status = RideStatus.Abandoned;
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
}