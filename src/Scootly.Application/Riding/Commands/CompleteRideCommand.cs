namespace Scootly.Application.Riding.Commands;

public sealed class CompleteRideCommand
{
    public Guid RideId { get; }
    public Guid DriverId { get; }
    public double EndLatitude { get; }
    public double EndLongitude { get; }

    public CompleteRideCommand(Guid rideId, Guid driverId, double endLatitude, double endLongitude)
    {
        RideId = rideId;
        DriverId = driverId;
        EndLatitude = endLatitude;
        EndLongitude = endLongitude;
    }
}