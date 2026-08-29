namespace Scootly.Application.Riding.Commands;

public sealed class CompleteRideCommand
{
    public Guid RideId { get; }
    public double EndLatitude { get; }
    public double EndLongitude { get; }

    public CompleteRideCommand(Guid rideId, double endLatitude, double endLongitude)
    {
        RideId = rideId;
        EndLatitude = endLatitude;
        EndLongitude = endLongitude;
    }
}