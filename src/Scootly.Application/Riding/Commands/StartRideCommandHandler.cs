using Scootly.Application.Abstractions;
using Scootly.Domain.Common;
using Scootly.Domain.Riding;

namespace Scootly.Application.Riding.Commands;

public sealed class StartRideCommandHandler
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IClock _clock;

    public StartRideCommandHandler(IApplicationDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<Result> Handle(StartRideCommand command, CancellationToken cancellationToken = default)
    {
        var vehicle = _dbContext.Vehicles.FirstOrDefault(v => v.Id == command.VehicleId);

        if (vehicle is null)
            return Result.Failure("Araç bulunamadı.");

        vehicle.StartRide();

        var ride = new Ride(
            RideId.New(),
            command.DriverId,
            command.VehicleId,
            vehicle.Location,
            _clock.UtcNow);

        _dbContext.AddRide(ride);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}