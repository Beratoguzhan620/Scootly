using Scootly.Application.Abstractions;
using Scootly.Application.Common;
using Scootly.Domain.Common;

namespace Scootly.Application.Riding.Commands;

public sealed record ReserveVehicleCommand(Guid VehicleId, Guid DriverId);

public sealed class ReserveVehicleCommandHandler
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IRideRepository _rideRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ReserveVehicleCommandHandler(
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

    public Task<Result> Handle(ReserveVehicleCommand command, CancellationToken cancellationToken = default)
    {
        return OptimisticConcurrency.ExecuteAsync(_unitOfWork, async token =>
        {
            var vehicle = await _vehicleRepository.GetByIdAsync(command.VehicleId, token);

            if (vehicle is null)
                return Result.NotFound("Araç bulunamadı.");

            if (await _vehicleRepository.HasActiveReservationAsync(command.DriverId, token))
                return Result.Failure(ConstraintNames.ToUserMessage(ConstraintNames.OneActiveReservationPerDriver));

            if (await _rideRepository.HasActiveRideAsync(command.DriverId, token))
                return Result.Failure("Devam eden bir sürüşünüz varken yeni rezervasyon yapamazsınız.");

            vehicle.Reserve(command.DriverId, _clock.UtcNow);
            await _unitOfWork.SaveChangesAsync(token);

            return Result.Success();
        },
        conflictMessage: "Araç, siz kontrol ettikten sonra başka biri tarafından rezerve edildi. Lütfen tekrar deneyin.",
        cancellationToken);
    }
}
