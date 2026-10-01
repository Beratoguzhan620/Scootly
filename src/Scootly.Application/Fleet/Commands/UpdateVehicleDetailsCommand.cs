using Scootly.Application.Abstractions;
using Scootly.Application.Common;
using Scootly.Domain.Common;
using Scootly.Domain.Fleet;

namespace Scootly.Application.Fleet.Commands;

public sealed record UpdateVehicleDetailsCommand(Guid VehicleId, string Brand, int RangeKm);

/// <summary>Filo yöneticisi: araç marka/menzil bilgisini günceller.</summary>
public sealed class UpdateVehicleDetailsCommandHandler
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public UpdateVehicleDetailsCommandHandler(IVehicleRepository vehicleRepository, IUnitOfWork unitOfWork, IClock clock)
    {
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task<Result> Handle(UpdateVehicleDetailsCommand command, CancellationToken cancellationToken = default)
    {
        return OptimisticConcurrency.ExecuteAsync(_unitOfWork, async token =>
        {
            var vehicle = await _vehicleRepository.GetByIdAsync(command.VehicleId, token);

            if (vehicle is null)
                return Result.NotFound("Araç bulunamadı.");

            VehicleModel newModel;

            try
            {
                newModel = new VehicleModel(command.Brand, command.RangeKm);
            }
            catch (DomainException ex)
            {
                return Result.Validation(ex.Message);
            }

            try
            {
                vehicle.UpdateModel(newModel, _clock.UtcNow);
            }
            catch (DomainException ex)
            {
                return Result.Validation(ex.Message);
            }

            await _unitOfWork.SaveChangesAsync(token);

            return Result.Success();
        },
        conflictMessage: "Araç bu sırada değişti. Lütfen tekrar deneyin.",
        cancellationToken);
    }
}