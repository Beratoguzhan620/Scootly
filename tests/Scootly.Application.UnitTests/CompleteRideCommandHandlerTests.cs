using Scootly.Application.Abstractions;
using Scootly.Application.Riding.Commands;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding;
using Xunit;

namespace Scootly.Application.UnitTests;

public sealed class CompleteRideCommandHandlerTests
{
    [Fact]
    public async Task Aktif_Suruste_Complete_Cagrilinca_Basarili_Olmali()
    {
        var vehicle = new Vehicle(
            VehicleId.New(),
            new VehicleModel("Xiaomi", 25),
            new GeoPoint(41.0, 29.0),
            new BatteryLevel(80));

        vehicle.Reserve();
        vehicle.StartRide();

        var ride = new Ride(
            RideId.New(),
            Guid.NewGuid(),
            vehicle.Id,
            new GeoPoint(41.0, 29.0),
            DateTime.UtcNow.AddMinutes(-10));

        var vehicleRepository = new FakeVehicleRepository(vehicle);
        var rideRepository = new FakeRideRepository(ride);
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock(DateTime.UtcNow);

        var handler = new CompleteRideCommandHandler(rideRepository, vehicleRepository, unitOfWork, clock);
        var command = new CompleteRideCommand(ride.Id, 41.01, 29.01);

        var result = await handler.Handle(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(RideStatus.Completed, ride.Status);
        Assert.Equal(VehicleStatus.Available, vehicle.Status);
    }

    [Fact]
    public async Task Var_Olmayan_Suruste_Complete_Cagrilinca_Basarisiz_Donmeli()
    {
        var vehicleRepository = new FakeVehicleRepository(null);
        var rideRepository = new FakeRideRepository(null);
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock(DateTime.UtcNow);

        var handler = new CompleteRideCommandHandler(rideRepository, vehicleRepository, unitOfWork, clock);
        var command = new CompleteRideCommand(Guid.NewGuid(), 41.01, 29.01);

        var result = await handler.Handle(command);

        Assert.False(result.IsSuccess);
    }

    private sealed class FakeVehicleRepository : IVehicleRepository
    {
        private readonly Vehicle? _vehicle;

        public FakeVehicleRepository(Vehicle? vehicle)
        {
            _vehicle = vehicle;
        }

        public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_vehicle is not null && _vehicle.Id == id ? _vehicle : null);
        }

        public Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRideRepository : IRideRepository
    {
        private readonly Ride? _ride;

        public FakeRideRepository(Ride? ride)
        {
            _ride = ride;
        }

        public Task<Ride?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_ride is not null && _ride.Id == id ? _ride : null);
        }

        public Task AddAsync(Ride ride, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(1);
        }
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }
}