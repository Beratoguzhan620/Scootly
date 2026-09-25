using Scootly.Domain.Fleet;
using Scootly.Domain.Riding;
using Scootly.Domain.Telemetry;

namespace Scootly.Application.Abstractions;

public interface IApplicationDbContext
{
    IQueryable<Vehicle> Vehicles { get; }
    IQueryable<Ride> Rides { get; }
    IQueryable<TelemetryReading> TelemetryReadings { get; }

    void AddVehicle(Vehicle vehicle);
    void AddRide(Ride ride);
    void AddTelemetryReading(TelemetryReading reading);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}