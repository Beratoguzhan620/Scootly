using Scootly.Application.Abstractions;
using Scootly.Domain.Common;

namespace Scootly.Application.Riding.Commands;

public sealed class CompleteRideCommandHandler
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CompleteRideCommandHandler(IApplicationDbContext context, IUnitOfWork unitOfWork, IClock clock)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result> Handle(CompleteRideCommand command, CancellationToken cancellationToken = default)
    {
        var ride = _context.Rides.FirstOrDefault(r => r.Id == command.RideId);

        if (ride is null)
            return Result.Failure("Sürüş bulunamadı.");

        var vehicle = _context.Vehicles.FirstOrDefault(v => v.Id == ride.VehicleId.Value);

        if (vehicle is null)
            return Result.Failure("Sürüşe ait araç bulunamadı.");

        ride.Complete(command.EndLocation, _clock.UtcNow);
        vehicle.CompleteRide();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}