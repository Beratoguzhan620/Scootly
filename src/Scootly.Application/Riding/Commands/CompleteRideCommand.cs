using Scootly.Application.Abstractions;
using Scootly.Application.Common;
using Scootly.Domain.Common;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding;

namespace Scootly.Application.Riding.Commands;

public sealed record CompleteRideCommand(Guid RideId, Guid DriverId, double EndLatitude, double EndLongitude);

public sealed record CompletedRide(Guid RideId, decimal Fare, PaymentStatus PaymentStatus);

public sealed class CompleteRideCommandHandler
{
    private readonly IRideRepository _rideRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CompleteRideCommandHandler(
        IRideRepository rideRepository,
        IVehicleRepository vehicleRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _rideRepository = rideRepository;
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task<Result<CompletedRide>> Handle(CompleteRideCommand command, CancellationToken cancellationToken = default)
    {
        return OptimisticConcurrency.ExecuteAsync(_unitOfWork, async token =>
        {
            var ride = await _rideRepository.GetByIdAsync(command.RideId, token);

            // Başkasına ait sürüşün varlığı açığa çıkarılmaz: sahibi değilse "bulunamadı".
            if (ride is null || ride.DriverId != command.DriverId)
                return Result<CompletedRide>.NotFound("Sürüş bulunamadı.");

            var vehicle = await _vehicleRepository.GetByIdAsync(ride.VehicleId, token);

            if (vehicle is null)
                return Result<CompletedRide>.NotFound("Araç bulunamadı.");

            var now = _clock.UtcNow;
            var endLocation = new GeoPoint(command.EndLatitude, command.EndLongitude);

            ride.Complete(endLocation, now, Tariff.Standard);
            vehicle.CompleteRide(endLocation.Copy(), now);

            await _unitOfWork.SaveChangesAsync(token);

            return Result<CompletedRide>.Success(new CompletedRide(ride.Id, ride.Fare!.Value, ride.PaymentStatus));
        },
        conflictMessage: "Sürüş bu sırada değişti. Lütfen tekrar deneyin.",
        cancellationToken);
    }
}
