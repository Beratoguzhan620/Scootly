using System;
using System.Collections.Generic;
using System.Text;

using Scootly.Domain.Common;

namespace Scootly.Domain.Riding.Events;

public sealed class RideCompletedEvent : IDomainEvent
{
    public RideId RideId { get; }
    public TimeSpan Duration { get; }
    public double DistanceMeters { get; }
    public DateTime OccurredOn { get; }

    public RideCompletedEvent(RideId rideId, TimeSpan duration, double distanceMeters, DateTime occurredOn)
    {
        RideId = rideId;
        Duration = duration;
        DistanceMeters = distanceMeters;
        OccurredOn = occurredOn;
    }
}
