using Scootly.Application.Abstractions;
using Scootly.Domain.Common;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding;

namespace Scootly.Application.Riding.Commands;

public sealed class CompleteRideCommandHandler
{
    private readonly IRideRepository _rides;
    private readonly IVehicleRepository _vehicles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CompleteRideCommandHandler(
        IRideRepository rides,
        IVehicleRepository vehicles,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _rides = rides;
        _vehicles = vehicles;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result> Handle(CompleteRideCommand command, CancellationToken cancellationToken = default)
    {
        var ride = await _rides.GetByIdAsync(new RideId(command.RideId), cancellationToken);

        // Sürüş yok VEYA çağıran kişinin değil — ikisi de aynı yanıtı döner.
        // Farklı mesaj dönseydi saldırgan hangi sürüş kimliklerinin var olduğunu
        // deneme yanılmayla çıkarabilirdi (enumeration).
        if (ride is null || ride.DriverId != command.DriverId)
            return Result.Failure("Sürüş bulunamadı.");

        var vehicle = await _vehicles.GetByIdAsync(new VehicleId(ride.VehicleId), cancellationToken);

        if (vehicle is null)
            return Result.Failure("Sürüş bulunamadı.");

        var endLocation = new GeoPoint(command.EndLatitude, command.EndLongitude);

        ride.Complete(endLocation, _clock.UtcNow);
        vehicle.CompleteRide();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}