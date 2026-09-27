using Scootly.Application.Abstractions;
using Scootly.Application.Common;
using Scootly.Domain.Common;

namespace Scootly.Application.Fleet.Commands;

public sealed record SendVehicleToMaintenanceCommand(Guid VehicleId);

public sealed record ReturnVehicleToServiceCommand(Guid VehicleId);

/// <summary>Filo operasyonu: aracı bakıma alma ve bakımdan hizmete döndürme.</summary>
public sealed class VehicleMaintenanceCommandHandler
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public VehicleMaintenanceCommandHandler(IVehicleRepository vehicleRepository, IUnitOfWork unitOfWork, IClock clock)
    {
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task<Result> Handle(SendVehicleToMaintenanceCommand command, CancellationToken cancellationToken = default)
        => ChangeAsync(command.VehicleId, (vehicle, now) => vehicle.SendToMaintenance(now), cancellationToken);

    public Task<Result> Handle(ReturnVehicleToServiceCommand command, CancellationToken cancellationToken = default)
        => ChangeAsync(command.VehicleId, (vehicle, now) => vehicle.ReturnToService(now), cancellationToken);

    private Task<Result> ChangeAsync(
        Guid vehicleId,
        Action<Domain.Fleet.Vehicle, DateTime> change,
        CancellationToken cancellationToken)
    {
        return OptimisticConcurrency.ExecuteAsync(_unitOfWork, async token =>
        {
            var vehicle = await _vehicleRepository.GetByIdAsync(vehicleId, token);

            if (vehicle is null)
                return Result.NotFound("Araç bulunamadı.");

            change(vehicle, _clock.UtcNow);
            await _unitOfWork.SaveChangesAsync(token);

            return Result.Success();
        },
        conflictMessage: "Araç bu sırada değişti. Lütfen tekrar deneyin.",
        cancellationToken);
    }
}
