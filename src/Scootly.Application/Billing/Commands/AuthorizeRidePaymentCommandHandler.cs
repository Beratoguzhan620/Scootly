using Scootly.Application.Abstractions;
using Scootly.Application.IntegrationEvents;
using Scootly.Domain.Common;

namespace Scootly.Application.Billing.Commands;

/// <summary>
/// Sürüşün ödemesini ister ve sonucu olay olarak yayınlar (69. gün — saga'nın 3. adımı).
/// </summary>
/// <remarks>
/// <para>
/// <b>Reddedilen ödeme bir HATA değil.</b> Sağlayıcı "kart reddedildi"
/// dediğinde bu adım başarıyla biter ve <c>Success = false</c> olan bir
/// sonuç olayı üretir; kararı bir sonraki adım (telafi) verir. Yalnızca
/// sağlayıcıya ULAŞILAMAMASI (zaman aşımı, bağlantı) istisnadır ve mesaj
/// yeniden denenir.
/// </para>
/// <para>
/// Sonuç ödeme bilgisinden değil sürüşten kontrol ediliyor: sürüşün ödemesi
/// zaten sonuçlanmışsa sağlayıcı ikinci kez ÇAĞRILMIYOR.
/// </para>
/// </remarks>
public sealed class AuthorizeRidePaymentCommandHandler
{
    private readonly IRideRepository _rideRepository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IOutboxWriter _outbox;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public AuthorizeRidePaymentCommandHandler(
        IRideRepository rideRepository,
        IPaymentGateway paymentGateway,
        IOutboxWriter outbox,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _rideRepository = rideRepository;
        _paymentGateway = paymentGateway;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result> Handle(AuthorizeRidePaymentCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var ride = await _rideRepository.GetByIdAsync(command.RideId, cancellationToken);

        if (ride is null)
            return Result.Failure("Sürüş bulunamadı.");

        if (ride.Fare is null)
            return Result.Failure("Ücreti hesaplanmamış sürüş için ödeme istenemez.");

        if (ride.PaymentStatus is not null)
            return Result.Success();

        // Tutar mesajdan degil surusten: mesajdaki tutar bir kopya.
        var sonuc = await _paymentGateway.AuthorizeAsync(
            new PaymentAuthorizationRequest(IdempotencyKey: ride.Id, DriverId: ride.DriverId, Amount: ride.Fare.Value),
            cancellationToken);

        await _outbox.WriteAsync(
            new PaymentAuthorizedIntegrationEvent(
                EventId: Guid.NewGuid(),
                OccurredOnUtc: _clock.UtcNow,
                RideId: ride.Id,
                Amount: ride.Fare.Value,
                Success: sonuc.Approved,
                FailureReason: sonuc.Approved ? null : sonuc.DeclineReason),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
