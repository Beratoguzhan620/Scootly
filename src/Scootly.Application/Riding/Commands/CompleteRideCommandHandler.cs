using Scootly.Application.Abstractions;
using Scootly.Application.IntegrationEvents;
using Scootly.Domain.Common;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding.Events;

namespace Scootly.Application.Riding.Commands;

/// <summary>Sürüşü bitirir ve saga'yı başlatır.</summary>
/// <remarks>
/// <para>
/// <b>66. gün — outbox.</b> Hafta 13'te olay kayıttan SONRA doğrudan
/// kuyruğa gönderiliyordu ve bu, ikili yazma problemiydi: kayıt başarılı olup
/// yayınlama başarısız olursa sürüşün ücreti hiç hesaplanmazdı. Artık olay
/// sürüşle AYNI transaction'da outbox tablosuna yazılıyor. Ya ikisi birden
/// kaydedilir ya hiçbiri; kuyruğa taşımak Worker'daki göndericinin işi.
/// Sonuç: RabbitMQ kapalıyken de sürüş bitirilebiliyor ve olay kaybolmuyor,
/// yalnızca gecikiyor.
/// </para>
/// <para>
/// <b>69. gün — araç burada serbest BIRAKILMIYOR.</b> Araç ödeme sonuçlanana
/// kadar "sürüşte" kalıyor; serbest bırakmak saga'nın son adımının
/// (<c>SettleRidePaymentCommandHandler</c>) işi. Bedeli: sürüş bitirildikten
/// sonra araç birkaç saniye haritada görünmüyor, ve Worker çalışmıyorsa
/// görünmemeye devam ediyor (teknik borç listesinde).
/// </para>
/// </remarks>
public sealed class CompleteRideCommandHandler
{
    private readonly IRideRepository _rideRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IOutboxWriter _outbox;

    public CompleteRideCommandHandler(
        IRideRepository rideRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        IOutboxWriter outbox)
    {
        _rideRepository = rideRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _outbox = outbox;
    }

    public async Task<Result> Handle(CompleteRideCommand command, CancellationToken cancellationToken = default)
    {
        var ride = await _rideRepository.GetByIdAsync(command.RideId, cancellationToken);

        if (ride is null)
            return Result.Failure("Sürüş bulunamadı.");

        var endLocation = new GeoPoint(command.EndLatitude, command.EndLongitude);

        ride.Complete(endLocation, _clock.UtcNow);

        var tamamlandi = ride.DomainEvents.OfType<RideCompletedEvent>().Single();

        await _outbox.WriteAsync(tamamlandi.ToIntegrationEvent(ride), cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        ride.ClearDomainEvents();

        return Result.Success();
    }
}
