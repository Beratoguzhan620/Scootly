using Scootly.Application.Abstractions;
using Scootly.Application.Riding.Commands;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding;
using Xunit;

namespace Scootly.Application.UnitTests;

public sealed class StartRideCommandHandlerTests
{
    [Fact]
    public async Task Rezerve_Aracta_StartRide_Cagrilinca_Durum_InRide_Olmali()
    {
        var vehicle = new Vehicle(
            VehicleId.New(),
            new VehicleModel("Xiaomi", 25),
            new GeoPoint(41.0, 29.0),
            new BatteryLevel(80));

        vehicle.Reserve();

        var vehicleRepository = new FakeVehicleRepository(vehicle);
        var rideRepository = new FakeRideRepository();
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock(DateTime.UtcNow);

        var handler = new StartRideCommandHandler(vehicleRepository, rideRepository, unitOfWork, clock);
        var command = new StartRideCommand(vehicle.Id, Guid.NewGuid());

        var result = await handler.Handle(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(VehicleStatus.InRide, vehicle.Status);
    }

    private sealed class FakeVehicleRepository : IVehicleRepository
    {
        private readonly Vehicle _vehicle;

        public FakeVehicleRepository(Vehicle vehicle)
        {
            _vehicle = vehicle;
        }

        public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_vehicle.Id == id ? _vehicle : null);
        }

        public Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRideRepository : IRideRepository
    {
        public Task<Ride?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Ride?>(null);
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