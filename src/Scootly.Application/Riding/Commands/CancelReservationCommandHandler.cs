using Scootly.Application.Abstractions;
using Scootly.Domain.Common;

namespace Scootly.Application.Riding.Commands;

public sealed class CancelReservationCommandHandler
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelReservationCommandHandler(IVehicleRepository vehicleRepository, IUnitOfWork unitOfWork)
    {
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(CancelReservationCommand command, CancellationToken cancellationToken = default)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(command.VehicleId, cancellationToken);

        if (vehicle is null)
            return Result.Failure("Araç bulunamadı.");

        vehicle.CancelReservation();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}