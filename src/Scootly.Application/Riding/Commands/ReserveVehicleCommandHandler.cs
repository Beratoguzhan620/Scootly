using Scootly.Application.Abstractions;
using Scootly.Domain.Common;
using Scootly.Domain.Fleet;

namespace Scootly.Application.Riding.Commands;

public sealed class ReserveVehicleCommandHandler
{
    private readonly IVehicleRepository _vehicles;
    private readonly IUnitOfWork _unitOfWork;

    public ReserveVehicleCommandHandler(IVehicleRepository vehicles, IUnitOfWork unitOfWork)
    {
        _vehicles = vehicles;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ReserveVehicleCommand command, CancellationToken cancellationToken = default)
    {
        if (command.DriverId == Guid.Empty)
            return Result.Failure("Geçersiz istek.");

        var vehicle = await _vehicles.GetByIdAsync(new VehicleId(command.VehicleId), cancellationToken);

        if (vehicle is null)
            return Result.Failure("Araç bulunamadı.");

        vehicle.Reserve();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}