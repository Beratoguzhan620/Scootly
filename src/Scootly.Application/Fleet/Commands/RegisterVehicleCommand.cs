using Scootly.Application.Abstractions;
using Scootly.Domain.Common;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;

namespace Scootly.Application.Fleet.Commands;

public sealed record RegisterVehicleCommand(
    string Brand,
    int RangeKm,
    double Latitude,
    double Longitude,
    int BatteryPercentage);

public sealed class RegisterVehicleCommandHandler
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RegisterVehicleCommandHandler(IVehicleRepository vehicleRepository, IUnitOfWork unitOfWork, IClock clock)
    {
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <returns>Başarılıysa kaydedilen aracın kimliği.</returns>
    public async Task<Result<Guid>> Handle(RegisterVehicleCommand command, CancellationToken cancellationToken = default)
    {
        Vehicle vehicle;

        try
        {
            vehicle = new Vehicle(
                VehicleId.New(),
                new VehicleModel(command.Brand, command.RangeKm),
                new GeoPoint(command.Latitude, command.Longitude),
                new BatteryLevel(command.BatteryPercentage),
                _clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Validation(ex.Message);
        }

        await _vehicleRepository.AddAsync(vehicle, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(vehicle.Id);
    }
}
