using Scootly.Application.Abstractions;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding;

namespace Scootly.Infrastructure.Persistence;

// GEÇİCİ: Hafta 3'te EF Core ile değiştirilecek.
public sealed class InMemoryApplicationDbContext : IApplicationDbContext, IUnitOfWork
{
    private readonly List<Vehicle> _vehicles = new();
    private readonly List<Ride> _rides = new();

    public InMemoryApplicationDbContext()
    {
        _vehicles.Add(new Vehicle(
            VehicleId.New(),
            new VehicleModel("Segway", 40),
            new GeoPoint(37.0000, 35.3213),
            new BatteryLevel(85)));

        _vehicles.Add(new Vehicle(
            VehicleId.New(),
            new VehicleModel("Ninebot", 30),
            new GeoPoint(37.0025, 35.3250),
            new BatteryLevel(42)));

        _vehicles.Add(new Vehicle(
            VehicleId.New(),
            new VehicleModel("Xiaomi", 25),
            new GeoPoint(37.0500, 35.4000),
            new BatteryLevel(20)));
    }

    public IQueryable<Vehicle> Vehicles => _vehicles.AsQueryable();
    public IQueryable<Ride> Rides => _rides.AsQueryable();

    public void AddVehicle(Vehicle vehicle) => _vehicles.Add(vehicle);
    public void AddRide(Ride ride) => _rides.Add(ride);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}