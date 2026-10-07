using Scootly.Domain.Common;

namespace Scootly.Domain.Fleet.Events;

public sealed record VehicleBatteryLowEvent(VehicleId VehicleId, int BatteryPercentage, DateTime OccurredOn) : IDomainEvent;
