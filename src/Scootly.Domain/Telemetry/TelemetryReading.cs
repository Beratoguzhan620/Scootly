using Scootly.Domain.Common;
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

    public TelemetryReading(Guid id, Guid vehicleId, GeoPoint location, int batteryPercentage, DateTime recordedAt)
        : base(id)
    {
        VehicleId = vehicleId;
        Location = location;
        BatteryPercentage = batteryPercentage;
        RecordedAt = recordedAt;
    }
}