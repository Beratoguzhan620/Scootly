using Scootly.Application.Abstractions;
using Scootly.Domain.Common;
using Scootly.Domain.Geo;

namespace Scootly.Application.Riding.Commands;

public sealed class CompleteRideCommandHandler
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IClock _clock;

    public CompleteRideCommandHandler(IApplicationDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<Result> Handle(CompleteRideCommand command, CancellationToken cancellationToken = default)
    {
        var ride = _dbContext.Rides.FirstOrDefault(r => r.Id == command.RideId);

        if (ride is null)
            return Result.Failure("Sürüş bulunamadı.");

        var vehicle = _dbContext.Vehicles.FirstOrDefault(v => v.Id == ride.VehicleId);

        if (vehicle is null)
            return Result.Failure("Araç bulunamadı.");

        var endLocation = new GeoPoint(command.EndLatitude, command.EndLongitude);

        ride.Complete(endLocation, _clock.UtcNow);
        vehicle.CompleteRide();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}