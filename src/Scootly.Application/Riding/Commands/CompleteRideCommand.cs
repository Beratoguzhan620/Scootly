using Scootly.Domain.Geo;

namespace Scootly.Application.Riding.Commands;

public sealed record CompleteRideCommand(Guid RideId, GeoPoint EndLocation);