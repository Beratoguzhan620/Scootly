using Scootly.Application.Abstractions;
using Scootly.Domain.Common;

namespace Scootly.Application.Riding.Commands;

public sealed class ReserveVehicleCommandHandler
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public ReserveVehicleCommandHandler(IApplicationDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ReserveVehicleCommand command, CancellationToken cancellationToken = default)
    {
        var vehicle = _context.Vehicles.FirstOrDefault(v => v.Id == command.VehicleId);

        if (vehicle is null)
            return Result.Failure("Araç bulunamadı.");

        vehicle.Reserve();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}