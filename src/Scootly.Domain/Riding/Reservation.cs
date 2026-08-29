namespace Scootly.Domain.Riding;

public sealed class Reservation
{
    public Guid VehicleId { get; }
    public Guid DriverId { get; }
    public DateTime CreatedAt { get; }
    public DateTime ExpiresAt { get; }

    public Reservation(Guid vehicleId, Guid driverId, DateTime createdAt, DateTime expiresAt)
    {
        VehicleId = vehicleId;
        DriverId = driverId;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }
}