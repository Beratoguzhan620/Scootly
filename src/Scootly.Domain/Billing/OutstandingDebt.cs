using Scootly.Domain.Common;
using Scootly.Domain.Riding;

namespace Scootly.Domain.Billing;

/// <summary>
/// Ödemesi alınamamış bir sürüşün borcu (69. gün — saga'nın telafi adımı).
/// </summary>
/// <remarks>
/// <para>
/// Ödeme başarısız olduğunda iki seçenek vardı: aracı kilitli tutup ödemenin
/// alınmasını beklemek, ya da aracı serbest bırakıp borcu kaydetmek. İlki
/// sürücüyü değil filoyu cezalandırıyor — bir kartın reddedilmesi yüzünden
/// kiralanabilir bir araç haritadan kaybolur ve bunu ancak bir müşteri
/// şikâyeti gösterir. Seçilen ikincisi: araç serbest, borç bu kayıtta.
/// </para>
/// <para>
/// Bir sürüş için en fazla BİR borç var; kural veritabanında da tekil indeksle
/// korunuyor (iki tüketici aynı sonucu aynı anda işlerse).
/// </para>
/// </remarks>
public sealed class OutstandingDebt : AggregateRoot
{
    public const int MaxReasonLength = 300;

    public Guid DriverId { get; private set; }
    public Guid RideId { get; private set; }
    public decimal Amount { get; private set; }
    public string Reason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? SettledAt { get; private set; }

    private OutstandingDebt()
    {
        Reason = string.Empty;
    }

    private OutstandingDebt(Guid id, Guid driverId, Guid rideId, decimal amount, string reason, DateTime createdAt)
        : base(id)
    {
        DriverId = driverId;
        RideId = rideId;
        Amount = amount;
        Reason = reason;
        CreatedAt = createdAt;
    }

    public static OutstandingDebt ForFailedRidePayment(Ride ride, string? reason, DateTime createdAt)
    {
        ArgumentNullException.ThrowIfNull(ride);

        if (ride.Fare is null || ride.Fare <= 0)
            throw new DomainException("Borç, ücreti hesaplanmış bir sürüş için açılabilir.");

        var sebep = string.IsNullOrWhiteSpace(reason) ? "Ödeme alınamadı." : reason.Trim();
        if (sebep.Length > MaxReasonLength)
            sebep = sebep[..MaxReasonLength];

        return new OutstandingDebt(Guid.NewGuid(), ride.DriverId, ride.Id, ride.Fare.Value, sebep, createdAt);
    }

    public void Settle(DateTime settledAt)
    {
        if (SettledAt is not null)
            throw new DomainException("Borç zaten kapatılmış.");

        SettledAt = settledAt;
    }
}
