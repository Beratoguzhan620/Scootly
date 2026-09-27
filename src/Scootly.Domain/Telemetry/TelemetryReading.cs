using Scootly.Domain.Common;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;

namespace Scootly.Domain.Telemetry;

public sealed class TelemetryReading : Entity
{
    public Guid VehicleId { get; private set; }
    public GeoPoint Location { get; private set; }
    public int BatteryPercentage { get; private set; }
    public DateTime RecordedAt { get; private set; }

    private TelemetryReading()
    {
        Location = null!;
    }

    public TelemetryReading(Guid id, Guid vehicleId, GeoPoint location, BatteryLevel battery, DateTime recordedAt)
        : base(id)
    {
        if (vehicleId == Guid.Empty)
            throw new DomainException("Telemetri kaydı için araç kimliği gerekli.");

        VehicleId = vehicleId;
        Location = location;
        BatteryPercentage = battery.Percentage;
        RecordedAt = recordedAt;
    }
}
