using Scootly.Domain.Common;

namespace Scootly.Domain.Fleet.Events;

public sealed class VehicleBatteryLowEvent : IDomainEvent
{
    public VehicleId VehicleId { get; }
    public int BatteryPercentage { get; }
    public DateTime OccurredOn { get; }

    public VehicleBatteryLowEvent(VehicleId vehicleId, int batteryPercentage, DateTime occurredOn)
    {
        VehicleId = vehicleId;
        BatteryPercentage = batteryPercentage;
        OccurredOn = occurredOn;
    }
}