using Scootly.Application.Abstractions;
using Scootly.Domain.Common;

namespace Scootly.Application.Riding.Commands;

public sealed class ReserveVehicleCommandHandler
{
    private readonly IApplicationDbContext _dbContext;

    public ReserveVehicleCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(ReserveVehicleCommand command, CancellationToken cancellationToken = default)
    {
        var vehicle = _dbContext.Vehicles.FirstOrDefault(v => v.Id == command.VehicleId);

        if (vehicle is null)
            return Result.Failure("Araç bulunamadı.");

        vehicle.Reserve();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}