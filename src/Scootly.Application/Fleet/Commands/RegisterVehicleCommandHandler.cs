using Scootly.Application.Abstractions;
using Scootly.Domain.Common;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;

namespace Scootly.Application.Fleet.Commands;

public sealed class RegisterVehicleCommandHandler
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterVehicleCommandHandler(IVehicleRepository vehicleRepository, IUnitOfWork unitOfWork)
    {
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(
        RegisterVehicleCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var id = VehicleId.New();

        var vehicle = new Vehicle(
            id,
            new VehicleModel(command.Brand, command.RangeKm),
            new GeoPoint(command.Latitude, command.Longitude),
            new BatteryLevel(command.BatteryPercentage));

        await _vehicleRepository.AddAsync(vehicle, cancellationToken);

        // Bu satır olmadan araç yalnızca EF'in takip listesine girer, veritabanına
        // hiç yazılmaz — ve hiçbir hata da vermez. Uç 200 döner, araç kaybolur.
        // Sessiz başarısızlık, gürültülü başarısızlıktan her zaman daha pahalıdır.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(id.Value);
    }
}
