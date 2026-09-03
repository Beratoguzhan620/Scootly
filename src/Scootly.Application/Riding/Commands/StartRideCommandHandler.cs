using Scootly.Application.Abstractions;
using Scootly.Domain.Common;
using Scootly.Domain.Fleet;
using Scootly.Domain.Riding;

namespace Scootly.Application.Riding.Commands;

public sealed class StartRideCommandHandler
{
    private readonly IVehicleRepository _vehicles;
    private readonly IRideRepository _rides;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public StartRideCommandHandler(
        IVehicleRepository vehicles,
        IRideRepository rides,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _vehicles = vehicles;
        _rides = rides;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result> Handle(StartRideCommand command, CancellationToken cancellationToken = default)
    {
        if (command.DriverId == Guid.Empty)
            return Result.Failure("Geçersiz istek.");

        var vehicle = await _vehicles.GetByIdAsync(new VehicleId(command.VehicleId), cancellationToken);

        if (vehicle is null)
            return Result.Failure("Araç bulunamadı.");

        vehicle.StartRide();

        var ride = new Ride(
            RideId.New(),
            command.DriverId,
            command.VehicleId,
            vehicle.Location,
            _clock.UtcNow);

        await _rides.AddAsync(ride, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}