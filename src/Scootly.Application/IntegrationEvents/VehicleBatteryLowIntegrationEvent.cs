namespace Scootly.Application.IntegrationEvents;

/// <summary>Bir aracın bataryası eşiğin altına düştü (63. gün).</summary>
public sealed record VehicleBatteryLowIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid VehicleId,
    int BatteryPercentage) : IIntegrationEvent, IHasEventName
{
    public static string EventName => "vehicle.battery-low";

    public void Validate()
    {
        if (EventId == Guid.Empty) throw new InvalidIntegrationEventException("EventId bos.");
        if (VehicleId == Guid.Empty) throw new InvalidIntegrationEventException("VehicleId bos.");
        if (BatteryPercentage is < 0 or > 100)
            throw new InvalidIntegrationEventException("BatteryPercentage 0-100 araliginda olmali.");
    }
}
