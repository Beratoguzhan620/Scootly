using Scootly.Application.Abstractions;
using Scootly.Domain.Common;
using Scootly.Domain.Fleet;
using Scootly.Domain.Riding;

namespace Scootly.Application.Riding.Commands;

public sealed class StartRideCommandHandler
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public StartRideCommandHandler(IApplicationDbContext context, IUnitOfWork unitOfWork, IClock clock)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(StartRideCommand command, CancellationToken cancellationToken = default)
    {
        var vehicle = _context.Vehicles.FirstOrDefault(v => v.Id == command.VehicleId);

        if (vehicle is null)
            return Result<Guid>.Failure("Araç bulunamadı.");

        vehicle.StartRide();

        var ride = new Ride(
            RideId.New(),
            command.DriverId,
            new VehicleId(vehicle.Id),
            vehicle.Location,
            _clock.UtcNow);

        _context.AddRide(ride);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(ride.Id);
    }
}