using Scootly.Application.Abstractions;
using Scootly.Application.Common;
using Scootly.Domain.Common;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
using Scootly.Domain.Telemetry;

namespace Scootly.Application.Telemetry;

public sealed record ProcessTelemetryBatchCommand(IReadOnlyList<TelemetryReadingData> Readings);

public sealed record TelemetryBatchResult(int Stored, int Skipped);

/// <summary>
/// Bir telemetri partisini tek transaction'da işler: ham okumaları saklar ve her aracın
/// son bilinen konum/bataryasını günceller (batarya eşiği aşılırsa domain olayı üretilir).
/// </summary>
public sealed class ProcessTelemetryBatchCommandHandler
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ITelemetryRepository _telemetryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProcessTelemetryBatchCommandHandler(
        IVehicleRepository vehicleRepository,
        ITelemetryRepository telemetryRepository,
        IUnitOfWork unitOfWork)
    {
        _vehicleRepository = vehicleRepository;
        _telemetryRepository = telemetryRepository;
        _unitOfWork = unitOfWork;
    }

    public Task<Result<TelemetryBatchResult>> Handle(ProcessTelemetryBatchCommand command, CancellationToken cancellationToken = default)
    {
        return OptimisticConcurrency.ExecuteAsync(_unitOfWork, async token =>
        {
            var vehicleIds = command.Readings.Select(r => r.VehicleId).Distinct().ToList();
            var vehicles = (await _vehicleRepository.GetByIdsAsync(vehicleIds, token)).ToDictionary(v => v.Id);

            var readings = new List<TelemetryReading>(command.Readings.Count);
            var skipped = 0;

            foreach (var data in command.Readings.OrderBy(r => r.RecordedAt))
            {
                if (!vehicles.TryGetValue(data.VehicleId, out var vehicle) || !TryCreate(data, out var location, out var battery))
                {
                    skipped++;
                    continue;
                }

                readings.Add(new TelemetryReading(Guid.NewGuid(), data.VehicleId, location, battery, data.RecordedAt));
                vehicle.ReportTelemetry(location.Copy(), battery, data.RecordedAt);
            }

            _telemetryRepository.AddRange(readings);
            await _unitOfWork.SaveChangesAsync(token);

            return Result<TelemetryBatchResult>.Success(new TelemetryBatchResult(readings.Count, skipped));
        },
        conflictMessage: "Telemetri partisi araç güncellemeleriyle tekrar tekrar çakıştı.",
        cancellationToken);
    }

    private static bool TryCreate(TelemetryReadingData data, out GeoPoint location, out BatteryLevel battery)
    {
        try
        {
            location = new GeoPoint(data.Latitude, data.Longitude);
            battery = new BatteryLevel(data.BatteryPercentage);
            return true;
        }
        catch (DomainException)
        {
            location = null!;
            battery = null!;
            return false;
        }
    }
}
