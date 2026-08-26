using Scootly.Domain.Common;

namespace Scootly.Domain.Riding.Events;

public sealed class RideStartedEvent : IDomainEvent
{
    public RideId RideId { get; }
    public DateTime OccurredOn { get; }

    public RideStartedEvent(RideId rideId, DateTime occurredOn)
    {
        RideId = rideId;
        OccurredOn = occurredOn;
    }
}