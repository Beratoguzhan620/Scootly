using Scootly.Domain.Common;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding.Events;

namespace Scootly.Domain.Riding;

public sealed class Ride : AggregateRoot
{
    public const int MaxPaymentAttempts = 5;
    public const int PaymentErrorMaxLength = 500;

    public Guid DriverId { get; }
    public Guid VehicleId { get; }

    /// <summary>Saklama süresi dolduğunda anonimleştirilir (null olur), bkz. ADR 0004.</summary>
    public GeoPoint? StartLocation { get; private set; }

    public GeoPoint? EndLocation { get; private set; }
    public DateTime StartedAt { get; }
    public DateTime? EndedAt { get; private set; }
    public RideStatus Status { get; private set; }
    public decimal? Fare { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; }
    public int PaymentAttempts { get; private set; }
    public string? LastPaymentError { get; private set; }
    public DateTime? LastPaymentAttemptAt { get; private set; }
    public DateTime? PaidAt { get; private set; }

    private Ride() { }

    public Ride(RideId id, Guid driverId, Guid vehicleId, GeoPoint startLocation, DateTime startedAt)
        : base(id.Value)
    {
        if (driverId == Guid.Empty)
            throw new DomainException("Sürüş için sürücü kimliği gerekli.");

        if (vehicleId == Guid.Empty)
            throw new DomainException("Sürüş için araç kimliği gerekli.");

        DriverId = driverId;
        VehicleId = vehicleId;
        StartLocation = startLocation;
        StartedAt = startedAt;
        Status = RideStatus.Active;
        PaymentStatus = PaymentStatus.None;

        AddDomainEvent(new RideStartedEvent(id, driverId, vehicleId, startedAt));
    }

    public TimeSpan? Duration => EndedAt - StartedAt;

    public void Complete(GeoPoint endLocation, DateTime endedAt, Tariff tariff)
    {
        EnsureActive("Yalnızca aktif bir sürüş tamamlanabilir.");
        EnsureNotBeforeStart(endedAt);

        EndLocation = endLocation;
        EndedAt = endedAt;
        Status = RideStatus.Completed;

        var duration = endedAt - StartedAt;
        var distanceMeters = StartLocation?.DistanceTo(endLocation) ?? 0;

        RequestPayment(tariff.Calculate(duration));

        AddDomainEvent(new RideCompletedEvent(
            new RideId(Id), DriverId, VehicleId, duration, distanceMeters, Fare!.Value, endedAt));
    }

    /// <summary>
    /// Sürücünün kapatmadığı, eşik süreyi aşmış sürüşü sistem kapatır.
    /// Geçen süre normal tarifeyle ücretlendirilir; araç son bilinen konumunda kalır.
    /// </summary>
    public void Abandon(GeoPoint lastKnownLocation, DateTime now, Tariff tariff)
    {
        EnsureActive("Yalnızca aktif bir sürüş terk edilmiş sayılabilir.");
        EnsureNotBeforeStart(now);

        EndLocation = lastKnownLocation;
        EndedAt = now;
        Status = RideStatus.Abandoned;

        var duration = now - StartedAt;
        RequestPayment(tariff.Calculate(duration));

        AddDomainEvent(new RideAbandonedEvent(new RideId(Id), DriverId, VehicleId, duration, Fare!.Value, now));
    }

    /// <summary>Ödeme sağlayıcısına gönderilecek bir sonraki denemenin idempotency anahtarı.</summary>
    public string NextPaymentIdempotencyKey => $"ride-{Id:N}-attempt-{PaymentAttempts + 1}";

    /// <summary>
    /// Ödeme onayını işler. Tekrarlanan onaylar etkisizdir; sağlayıcının geç gelen onayı (webhook)
    /// "Failed" durumundaki bir sürüşü de ödenmiş sayar.
    /// </summary>
    public void RecordPaymentApproved(DateTime now)
    {
        if (PaymentStatus == PaymentStatus.Paid)
            return;

        if (PaymentStatus is not (PaymentStatus.Pending or PaymentStatus.Failed))
            throw new DomainException("Bu sürüş için bekleyen bir ödeme yok.");

        PaymentAttempts++;
        LastPaymentAttemptAt = now;
        LastPaymentError = null;
        PaymentStatus = PaymentStatus.Paid;
        PaidAt = now;
    }

    public void RecordPaymentDeclined(string reason, DateTime now)
    {
        EnsurePaymentPending();

        PaymentAttempts++;
        LastPaymentAttemptAt = now;
        LastPaymentError = Truncate(reason, PaymentErrorMaxLength);

        if (PaymentAttempts >= MaxPaymentAttempts)
            PaymentStatus = PaymentStatus.Failed;
    }

    private void RequestPayment(decimal fare)
    {
        Fare = fare;
        PaymentStatus = PaymentStatus.Pending;
    }

    private void EnsureActive(string errorMessage)
    {
        if (Status != RideStatus.Active)
            throw new DomainException(errorMessage);
    }

    private void EnsureNotBeforeStart(DateTime moment)
    {
        if (moment < StartedAt)
            throw new DomainException("Bitiş zamanı başlangıç zamanından önce olamaz.");
    }

    private void EnsurePaymentPending()
    {
        if (PaymentStatus != PaymentStatus.Pending)
            throw new DomainException("Bu sürüş için bekleyen bir ödeme yok.");
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
