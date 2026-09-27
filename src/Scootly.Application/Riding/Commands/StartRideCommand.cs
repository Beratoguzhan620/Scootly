using Scootly.Application.Abstractions;
using Scootly.Application.Common;
using Scootly.Domain.Common;
using Scootly.Domain.Riding;

namespace Scootly.Application.Riding.Commands;

public sealed record StartRideCommand(Guid VehicleId, Guid DriverId);

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

    /// <returns>Başarılıysa oluşturulan sürüşün kimliği.</returns>
    public Task<Result<Guid>> Handle(StartRideCommand command, CancellationToken cancellationToken = default)
    {
        return OptimisticConcurrency.ExecuteAsync(_unitOfWork, async token =>
        {
            var vehicle = await _vehicleRepository.GetByIdAsync(command.VehicleId, token);

            if (vehicle is null)
                return Result<Guid>.NotFound("Araç bulunamadı.");

            if (await _rideRepository.HasActiveRideAsync(command.DriverId, token))
                return Result<Guid>.Failure(ConstraintNames.ToUserMessage(ConstraintNames.OneActiveRidePerDriver));

            var now = _clock.UtcNow;

            vehicle.StartRide(command.DriverId, now);

            var ride = new Ride(RideId.New(), command.DriverId, vehicle.Id, vehicle.Location, now);

            await _rideRepository.AddAsync(ride, token);
            await _unitOfWork.SaveChangesAsync(token);

            return Result<Guid>.Success(ride.Id);
        },
        conflictMessage: "Araç bu sırada değişti. Lütfen tekrar deneyin.",
        cancellationToken);
    }
}
