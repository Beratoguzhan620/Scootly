using Microsoft.EntityFrameworkCore;
using Scootly.Application.Abstractions;
using Scootly.Domain.Common;

namespace Scootly.Application.Riding.Commands;

public sealed class ReserveVehicleCommandHandler
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReserveVehicleCommandHandler(IVehicleRepository vehicleRepository, IUnitOfWork unitOfWork)
    {
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ReserveVehicleCommand command, CancellationToken cancellationToken = default)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(command.VehicleId, cancellationToken);

        if (vehicle is null)
            return Result.Failure("Araç bulunamadı.");

        try
        {
            vehicle.Reserve();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure("Araç, siz kontrol ettikten sonra başka biri tarafından rezerve edildi. Lütfen tekrar deneyin.");
        }

        return Result.Success();
    }
}