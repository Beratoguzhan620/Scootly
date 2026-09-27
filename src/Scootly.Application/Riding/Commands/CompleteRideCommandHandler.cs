using Microsoft.Extensions.Logging;
using Scootly.Application.Abstractions;
using Scootly.Application.IntegrationEvents;
using Scootly.Domain.Common;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding.Events;

namespace Scootly.Application.Riding.Commands;

public sealed class CompleteRideCommandHandler
{
    private readonly IRideRepository _rideRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<CompleteRideCommandHandler> _logger;

    public CompleteRideCommandHandler(
        IRideRepository rideRepository,
        IVehicleRepository vehicleRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        IEventPublisher eventPublisher,
        ILogger<CompleteRideCommandHandler> logger)
    {
        _rideRepository = rideRepository;
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<Result> Handle(CompleteRideCommand command, CancellationToken cancellationToken = default)
    {
        var ride = await _rideRepository.GetByIdAsync(command.RideId, cancellationToken);

        if (ride is null)
            return Result.Failure("Sürüş bulunamadı.");

        var vehicle = await _vehicleRepository.GetByIdAsync(ride.VehicleId, cancellationToken);

        if (vehicle is null)
            return Result.Failure("Araç bulunamadı.");

        var endLocation = new GeoPoint(command.EndLatitude, command.EndLongitude);

        ride.Complete(endLocation, _clock.UtcNow);
        vehicle.CompleteRide();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // --- 62-63. gün: sürüş bitti, ücret hesabı artık bu isteğin işi değil ---
        //
        // Olay KAYITTAN SONRA yayınlanıyor. Önce yayınlansaydı, kayıt başarısız
        // olduğunda tüketici var olmayan bir tamamlanmış sürüş için ücret
        // hesaplamaya çalışırdı.
        //
        // BİLİNEN AÇIK — İKİLİ YAZMA PROBLEMİ: veritabanı ile kuyruk iki ayrı
        // sistem ve bu iki yazma birlikte garanti edilemiyor. Kayıt başarılı
        // olup yayınlama başarısız olursa (RabbitMQ kapalı, ağ koptu, süreç tam
        // bu satırda öldü) sürüş tamamlanmış ama ücreti HİÇ hesaplanmayacak.
        // Aşağıdaki catch bunu çözmüyor, yalnızca görünür kılıyor. Çözüm 66.
        // günün outbox deseni: olay, sürüşle AYNI transaction'da veritabanına
        // yazılacak ve ayrı bir servis kuyruğa taşıyacak.
        //
        // Yayınlama hatası isteği başarısız YAPMIYOR: sürüş gerçekten tamamlandı
        // ve veritabanında öyle duruyor. Kullanıcıya "hata" demek, tekrar
        // denemesine ve ikinci denemede "yalnızca aktif bir sürüş tamamlanabilir"
        // hatası almasına yol açardı.
        var tamamlandi = ride.DomainEvents.OfType<RideCompletedEvent>().SingleOrDefault();

        if (tamamlandi is not null)
        {
            var entegrasyonOlayi = tamamlandi.ToIntegrationEvent(ride);

            try
            {
                await _eventPublisher.PublishAsync(entegrasyonOlayi, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "RideCompleted olayi yayinlanamadi. Surus {RideId} tamamlandi ama ucreti HESAPLANMAYACAK. " +
                    "EventId={EventId}. Bu, 66. gunun outbox'inin kapatacagi ikili yazma acigi.",
                    ride.Id, entegrasyonOlayi.EventId);
            }
        }

        ride.ClearDomainEvents();

        return Result.Success();
    }
}
