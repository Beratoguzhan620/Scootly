using Scootly.Application.Abstractions;
using Scootly.Application.Common;
using Scootly.Domain.Common;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding;

namespace Scootly.Application.Riding.Commands;

public sealed class StartRideCommandHandler
{
    public const string CakismaMesaji = "Biri sizden önce davrandı; araç artık müsait değil.";

    private readonly IVehicleRepository _vehicleRepository;
    private readonly IRideRepository _rideRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly INearbyVehicleCache _nearbyCache;

    public StartRideCommandHandler(
        IVehicleRepository vehicleRepository,
        IRideRepository rideRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        INearbyVehicleCache nearbyCache)
    {
        _vehicleRepository = vehicleRepository;
        _rideRepository = rideRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _nearbyCache = nearbyCache;
    }

    public async Task<Result> Handle(StartRideCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vehicle = await _vehicleRepository.GetByIdAsync(command.VehicleId, cancellationToken);

        if (vehicle is null)
        {
            return Result.Failure("Araç bulunamadı.");
        }

        vehicle.StartRide();

        var startLocation = new GeoPoint(vehicle.Location.Latitude, vehicle.Location.Longitude);

        var ride = new Ride(
            RideId.New(),
            command.DriverId,
            command.VehicleId,
            startLocation,
            _clock.UtcNow);

        await _rideRepository.AddAsync(ride, cancellationToken);

        try
        {
            // Araç güncellemesi ve sürüş eklemesi TEK SaveChanges çağrısında,
            // yani tek bir işlem içinde. Ayrı ayrı kaydedilseydi araç "sürüşte"
            // olup ortada sürüş bulunmayan bir an doğardı — ve o an bir hata
            // olursa kalıcı hale gelirdi.
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result.Failure(CakismaMesaji);
        }

        // 49. gün — araç sürüşe geçti, haritada görünmemeli.
        await _nearbyCache.InvalidateAsync(cancellationToken);

        return Result.Success();
    }
}
