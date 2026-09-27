using Scootly.Application.Abstractions;
using Scootly.Application.Common;
using Scootly.Domain.Common;
using Scootly.Domain.Fleet;

namespace Scootly.Application.Riding.Commands;

/// <summary>Sürücünün kendi rezervasyonunu iptal etmesi.</summary>
public sealed record CancelReservationCommand(Guid VehicleId, Guid DriverId);

public sealed class CancelReservationCommandHandler
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CancelReservationCommandHandler(IVehicleRepository vehicleRepository, IUnitOfWork unitOfWork, IClock clock)
    {
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task<Result> Handle(CancelReservationCommand command, CancellationToken cancellationToken = default)
    {
        return OptimisticConcurrency.ExecuteAsync(_unitOfWork, async token =>
        {
            var vehicle = await _vehicleRepository.GetByIdAsync(command.VehicleId, token);

            // Başka birinin rezervasyonunun varlığını açığa çıkarmamak için aynı yanıt verilir.
            if (vehicle is null || vehicle.Status != VehicleStatus.Reserved || vehicle.ReservedBy != command.DriverId)
                return Result.NotFound("Bu araç için aktif bir rezervasyonunuz bulunamadı.");

            vehicle.CancelReservation(command.DriverId, _clock.UtcNow);
            await _unitOfWork.SaveChangesAsync(token);

            return Result.Success();
        },
        conflictMessage: "Rezervasyon bu sırada değişti. Lütfen tekrar deneyin.",
        cancellationToken);
    }
}
