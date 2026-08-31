using Scootly.Application.Abstractions;
using Scootly.Domain.Common;
using Scootly.Domain.Riding;

namespace Scootly.Application.Riding.Commands;

public sealed class StartRideCommandHandler
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IRideRepository _rideRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public StartRideCommandHandler(
        IVehicleRepository vehicleRepository,
        IRideRepository rideRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _vehicleRepository = vehicleRepository;
        _rideRepository = rideRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result> Handle(StartRideCommand command, CancellationToken cancellationToken = default)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(command.VehicleId, cancellationToken);

        if (vehicle is null)
            return Result.Failure("Araç bulunamadı.");

        vehicle.StartRide();

        var ride = new Ride(
            RideId.New(),
            command.DriverId,
            command.VehicleId,
            vehicle.Location,
            _clock.UtcNow);

        await _rideRepository.AddAsync(ride, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}