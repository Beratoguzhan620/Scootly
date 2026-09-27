using Scootly.Application.Abstractions;
using Scootly.Application.IntegrationEvents;
using Scootly.Domain.Common;
using Scootly.Domain.Pricing;
using Scootly.Domain.Riding;

namespace Scootly.Application.Pricing.Commands;

/// <summary>
/// Tamamlanmış bir sürüşün ücretini hesaplar ve ödemeyi ister
/// (64. gün; 69. günde saga'nın 2. adımı oldu).
/// </summary>
/// <remarks>
/// <para>
/// <b>Ücret, olaydaki süreden değil sürüşün kendisinden hesaplanıyor.</b>
/// Mesajdaki <c>DurationMinutes</c> bir kopya; kaynağı veritabanındaki
/// <c>Ride</c>. Kopyadan para hesaplamak, kopya ile kaynak bir gün ayrıştığında
/// yanlış ücret demek.
/// </para>
/// <para>
/// <b>Ücret ve ödeme isteği aynı transaction'da.</b> Ödeme isteği kuyruğa
/// doğrudan değil outbox'a yazılıyor; ücret kaydedilip ödeme isteği
/// kaybolamaz (ya da tersi).
/// </para>
/// <para>
/// Ücret zaten yazılmışsa başarı dönüyor ve ikinci bir ödeme isteği
/// ÜRETİLMİYOR. Tekrar gelen mesajların asıl koruması
/// <c>IdempotencyBehavior</c>; bu kontrol ikinci savunma hattı.
/// </para>
/// </remarks>
public sealed class ApplyRideFareCommandHandler
{
    private readonly IRideRepository _rideRepository;
    private readonly IOutboxWriter _outbox;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ApplyRideFareCommandHandler(
        IRideRepository rideRepository,
        IOutboxWriter outbox,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _rideRepository = rideRepository;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result> Handle(ApplyRideFareCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var ride = await _rideRepository.GetByIdAsync(command.RideId, cancellationToken);

        if (ride is null)
            return Result.Failure("Sürüş bulunamadı.");

        if (ride.Status != RideStatus.Completed || ride.EndedAt is null)
            return Result.Failure("Sürüş tamamlanmamış, ücret hesaplanamaz.");

        if (ride.Fare is not null)
            return Result.Success();

        var fare = Tariff.Standard.Calculate(ride.EndedAt.Value - ride.StartedAt);

        ride.ApplyFare(fare);

        await _outbox.WriteAsync(
            new PaymentAuthorizationRequestedIntegrationEvent(
                EventId: Guid.NewGuid(),
                OccurredOnUtc: _clock.UtcNow,
                RideId: ride.Id,
                DriverId: ride.DriverId,
                Amount: fare),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
