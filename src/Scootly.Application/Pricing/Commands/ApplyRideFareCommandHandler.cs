using Scootly.Application.Abstractions;
using Scootly.Domain.Common;
using Scootly.Domain.Pricing;
using Scootly.Domain.Riding;

namespace Scootly.Application.Pricing.Commands;

/// <summary>
/// Tamamlanmış bir sürüşün ücretini hesaplayıp yazar (64. gün).
/// </summary>
/// <remarks>
/// <para>
/// <b>Ücret, olaydaki süreden değil sürüşün kendisinden hesaplanıyor.</b>
/// Mesajdaki <c>DurationMinutes</c> bir kopya; kaynağı veritabanındaki
/// <c>Ride</c>. Kopyadan para hesaplamak, kopya ile kaynak bir gün ayrıştığında
/// (mesaj formatı değişti, yuvarlama farklı) yanlış ücret demek. Olay yalnızca
/// "bak, bu sürüş bitti" diyor.
/// </para>
/// <para>
/// <b>Ücret zaten yazılmışsa başarı dönüyor, hata değil.</b> Aynı mesaj iki kez
/// gelebilir (en az bir kez teslim) ve ikinci geliş bir hata değil, beklenen
/// bir durum. Bu kontrol 68. günün tam idempotency'sinin yerini TUTMUYOR:
/// iki tüketici aynı mesajı aynı anda işlerse ikisi de "ücret yok" görebilir.
/// Bugün tek tüketici var; 68. gün bunu kapatacak.
/// </para>
/// </remarks>
public sealed class ApplyRideFareCommandHandler
{
    private readonly IRideRepository _rideRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ApplyRideFareCommandHandler(IRideRepository rideRepository, IUnitOfWork unitOfWork)
    {
        _rideRepository = rideRepository;
        _unitOfWork = unitOfWork;
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

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
