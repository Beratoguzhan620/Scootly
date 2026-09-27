using Scootly.Application.Abstractions;
using Scootly.Application.Common;
using Scootly.Domain.Common;
using Scootly.Domain.Riding;

namespace Scootly.Application.Payments.Commands;

/// <summary>Sürüşün bekleyen ücretini ödeme sağlayıcısından tahsil etmeyi dener.</summary>
public sealed record ChargeRideCommand(Guid RideId);

public enum ChargeOutcome
{
    /// <summary>Bekleyen ödeme yok (zaten ödenmiş, başarısız sayılmış veya ücret henüz belirlenmemiş).</summary>
    NothingToCharge,
    Approved,
    Declined,

    /// <summary>Sağlayıcıya ulaşılamadı; deneme sayılmadı, daha sonra tekrar denenmeli.</summary>
    GatewayUnavailable
}

public sealed class ChargeRideCommandHandler
{
    private readonly IRideRepository _rideRepository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ChargeRideCommandHandler(
        IRideRepository rideRepository,
        IPaymentGateway paymentGateway,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _rideRepository = rideRepository;
        _paymentGateway = paymentGateway;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <remarks>
    /// Güvenli şekilde tekrarlanabilir: aynı deneme için sağlayıcıya aynı idempotency anahtarı gider,
    /// bu yüzden mesaj yeniden teslim edilse veya kayıt sırasında çakışma olsa bile ikinci tahsilat oluşmaz.
    /// </remarks>
    public Task<Result<ChargeOutcome>> Handle(ChargeRideCommand command, CancellationToken cancellationToken = default)
    {
        return OptimisticConcurrency.ExecuteAsync(_unitOfWork, async token =>
        {
            var ride = await _rideRepository.GetByIdAsync(command.RideId, token);

            if (ride is null)
                return Result<ChargeOutcome>.NotFound("Sürüş bulunamadı.");

            if (ride.PaymentStatus != PaymentStatus.Pending || ride.Fare is null)
                return Result<ChargeOutcome>.Success(ChargeOutcome.NothingToCharge);

            var request = new PaymentAuthorizationRequest(ride.Id, ride.Fare.Value, ride.NextPaymentIdempotencyKey);
            var gatewayResult = await _paymentGateway.AuthorizeAsync(request, token);

            switch (gatewayResult.Outcome)
            {
                case PaymentGatewayOutcome.Approved:
                    ride.RecordPaymentApproved(_clock.UtcNow);
                    await _unitOfWork.SaveChangesAsync(token);
                    return Result<ChargeOutcome>.Success(ChargeOutcome.Approved);

                case PaymentGatewayOutcome.Declined:
                    ride.RecordPaymentDeclined(gatewayResult.Message, _clock.UtcNow);
                    await _unitOfWork.SaveChangesAsync(token);
                    return Result<ChargeOutcome>.Success(ChargeOutcome.Declined);

                default:
                    return Result<ChargeOutcome>.Success(ChargeOutcome.GatewayUnavailable);
            }
        },
        conflictMessage: "Sürüşün ödeme durumu bu sırada değişti.",
        cancellationToken);
    }
}
