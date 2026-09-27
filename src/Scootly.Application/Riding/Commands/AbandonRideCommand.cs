using Scootly.Application.Abstractions;
using Scootly.Application.Common;
using Scootly.Domain.Common;
using Scootly.Domain.Fleet;
using Scootly.Domain.Riding;

namespace Scootly.Application.Riding.Commands;

/// <summary>Eşik süreyi aşmış aktif sürüşün sistem tarafından kapatılması (Worker).</summary>
public sealed record AbandonRideCommand(Guid RideId);

public sealed class AbandonRideCommandHandler
{
    private readonly IRideRepository _rideRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public AbandonRideCommandHandler(
        IRideRepository rideRepository,
        IVehicleRepository vehicleRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _rideRepository = rideRepository;
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task<Result> Handle(AbandonRideCommand command, CancellationToken cancellationToken = default)
    {
        return OptimisticConcurrency.ExecuteAsync(_unitOfWork, async token =>
        {
            var ride = await _rideRepository.GetByIdAsync(command.RideId, token);

            if (ride is null)
                return Result.NotFound("Sürüş bulunamadı.");

            var vehicle = await _vehicleRepository.GetByIdAsync(ride.VehicleId, token);
            var now = _clock.UtcNow;
            var lastKnownLocation = (vehicle?.Location ?? ride.StartLocation!).Copy();

            ride.Abandon(lastKnownLocation, now, Tariff.Standard);

            // Araç kilitli kalmasın: saha ekibi kontrol edene kadar bakıma alınır.
            if (vehicle is not null && vehicle.Status == VehicleStatus.InRide)
                vehicle.EndAbandonedRide(now);

            await _unitOfWork.SaveChangesAsync(token);

            return Result.Success();
        },
        conflictMessage: "Sürüş bu sırada değişti; bir sonraki turda yeniden değerlendirilecek.",
        cancellationToken);
    }
}
