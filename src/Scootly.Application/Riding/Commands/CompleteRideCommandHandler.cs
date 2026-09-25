using Scootly.Application.Abstractions;
using Scootly.Application.IntegrationEvents;
using Scootly.Domain.Common;
using Scootly.Domain.Geo;

namespace Scootly.Application.Riding.Commands;

public sealed class CompleteRideCommandHandler
{
    private readonly IRideRepository _rideRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IOutboxWriter _outboxWriter;

    public CompleteRideCommandHandler(
        IRideRepository rideRepository,
        IVehicleRepository vehicleRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        IOutboxWriter outboxWriter)
    {
        _rideRepository = rideRepository;
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _outboxWriter = outboxWriter;
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

        var durationMinutes = ride.EndedAt.HasValue
            ? (int)(ride.EndedAt.Value - ride.StartedAt).TotalMinutes
            : 0;

        var distanceMeters = ride.EndLocation is not null
            ? ride.StartLocation.DistanceTo(ride.EndLocation)
            : 0;

        var integrationEvent = new RideCompletedIntegrationEvent(
            ride.Id,
            ride.DriverId,
            ride.VehicleId,
            durationMinutes,
            distanceMeters);

        _outboxWriter.Write("RideCompleted", integrationEvent);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}