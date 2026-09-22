using Scootly.Application.Abstractions;
using Scootly.Domain.Common;
using Scootly.Domain.Geo;

namespace Scootly.Application.Riding.Commands;

public sealed class CompleteRideCommandHandler
{
    private readonly IRideRepository _rideRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly INearbyVehicleCache _nearbyCache;

    public CompleteRideCommandHandler(
        IRideRepository rideRepository,
        IVehicleRepository vehicleRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        INearbyVehicleCache nearbyCache)
    {
        _rideRepository = rideRepository;
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _nearbyCache = nearbyCache;
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

        // 49. gün — araç tekrar müsait. Bu yön diğer ikisinden daha önemli:
        // müsait olmayan bir aracı haritada göstermek kullanıcıyı boşuna
        // yürütür, müsait olan bir aracı GÖSTERMEMEK ise kiralanabilecek bir
        // aracı beş saniye boyunca gizler. İkisi de yanlış, ama ikincisi
        // doğrudan gelir kaybı.
        await _nearbyCache.InvalidateAsync(cancellationToken);

        return Result.Success();
    }
}