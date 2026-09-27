using Scootly.Application.Abstractions;
using Scootly.Application.Common;
using Scootly.Domain.Common;

namespace Scootly.Application.Riding.Commands;

/// <summary>Süresi dolan rezervasyonun sistem tarafından kaldırılması (Worker).</summary>
public sealed record ExpireReservationCommand(Guid VehicleId);

public sealed class ExpireReservationCommandHandler
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ExpireReservationCommandHandler(IVehicleRepository vehicleRepository, IUnitOfWork unitOfWork, IClock clock)
    {
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task<Result> Handle(ExpireReservationCommand command, CancellationToken cancellationToken = default)
    {
        return OptimisticConcurrency.ExecuteAsync(_unitOfWork, async token =>
        {
            var vehicle = await _vehicleRepository.GetByIdAsync(command.VehicleId, token);

            if (vehicle is null)
                return Result.NotFound("Araç bulunamadı.");

            vehicle.ExpireReservation(_clock.UtcNow);
            await _unitOfWork.SaveChangesAsync(token);

            return Result.Success();
        },
        conflictMessage: "Araç bu sırada değişti; bir sonraki turda yeniden değerlendirilecek.",
        cancellationToken);
    }
}
