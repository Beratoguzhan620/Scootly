using Scootly.Application.Abstractions;
using Scootly.Domain.Billing;
using Scootly.Domain.Common;
using Scootly.Domain.Fleet;

namespace Scootly.Application.Billing.Commands;

/// <summary>
/// Ödeme sonucunu işler ve aracı serbest bırakır (69. gün — saga'nın son adımı).
/// </summary>
/// <remarks>
/// <para>
/// <b>Araç her iki durumda da serbest kalıyor.</b> Ödeme başarılıysa sürüş
/// "ödendi"; başarısızsa TELAFİ: sürüş "ödeme alınamadı" olarak işaretleniyor,
/// sürücü adına borç açılıyor ve araç YİNE serbest bırakılıyor. Telafi
/// yazılmasaydı, reddedilen bir kart aracı sonsuza kadar "sürüşte" bırakırdı
/// — kimse kiralayamaz ve bunu ancak bir şikâyet gösterirdi.
/// </para>
/// <para>
/// <b>Telafi ≠ geri alma.</b> Sürüş olmuş bir şey; silinmiyor, iptal edilmiyor.
/// Telafi, olmuş olanın üstüne sistemi yeniden tutarlı hale getiren YENİ bir
/// işlem.
/// </para>
/// <para>
/// Araç yalnızca hâlâ "sürüşte" ise serbest bırakılıyor. Başka bir durumdaysa
/// (saha ekibi bakıma almış) ona dokunulmuyor — ödeme sonucu, bir operatörün
/// kararını ezmemeli.
/// </para>
/// </remarks>
public sealed class SettleRidePaymentCommandHandler
{
    private readonly IRideRepository _rideRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IOutstandingDebtRepository _debtRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public SettleRidePaymentCommandHandler(
        IRideRepository rideRepository,
        IVehicleRepository vehicleRepository,
        IOutstandingDebtRepository debtRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _rideRepository = rideRepository;
        _vehicleRepository = vehicleRepository;
        _debtRepository = debtRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result> Handle(SettleRidePaymentCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var ride = await _rideRepository.GetByIdAsync(command.RideId, cancellationToken);

        if (ride is null)
            return Result.Failure("Sürüş bulunamadı.");

        // Tekrar gelen sonuc: saga bu surus icin zaten bitmis. Araca da
        // dokunulmuyor — o arac bu arada baska birine kiralanmis olabilir.
        if (ride.PaymentStatus is not null)
            return Result.Success();

        try
        {
            if (command.Success)
            {
                ride.MarkPaid();
            }
            else
            {
                ride.MarkPaymentFailed();
                await _debtRepository.AddAsync(
                    OutstandingDebt.ForFailedRidePayment(ride, command.FailureReason, _clock.UtcNow),
                    cancellationToken);
            }
        }
        catch (DomainException ex)
        {
            return Result.Failure(ex.Message);
        }

        var vehicle = await _vehicleRepository.GetByIdAsync(ride.VehicleId, cancellationToken);

        if (vehicle is { Status: VehicleStatus.InRide })
            vehicle.CompleteRide();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
