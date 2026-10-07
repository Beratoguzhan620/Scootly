using Scootly.Application.Abstractions;
using Scootly.Application.Abstractions.Exceptions;
using Scootly.Domain.FieldOps;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding;
using Scootly.Domain.Telemetry;

namespace Scootly.Application.UnitTests;

public static class TestClock
{
    public static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
}

public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = TestClock.Now;
}

/// <summary>
/// EF'nin davranışını taklit eder: <see cref="DiscardChanges"/> sonrası varlıklar "veritabanından" (fabrikadan)
/// yeniden yüklenir; istenirse ilk N kayıt eşzamanlılık çakışması veya benzersizlik ihlaliyle başarısız olur.
/// </summary>
public sealed class FakeUnitOfWork : IUnitOfWork
{
    private readonly List<Action> _onDiscard = new();

    public int SaveCount { get; private set; }
    public int SuccessfulSaveCount { get; private set; }
    public int ConcurrencyConflictsToThrow { get; set; }
    public string? UniqueViolationToThrow { get; set; }

    public void OnDiscard(Action action) => _onDiscard.Add(action);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;

        if (ConcurrencyConflictsToThrow > 0)
        {
            ConcurrencyConflictsToThrow--;
            throw new ConcurrencyConflictException("test çakışması");
        }

        if (UniqueViolationToThrow is not null)
            throw new UniqueConstraintViolationException(UniqueViolationToThrow);

        SuccessfulSaveCount++;
        return Task.FromResult(1);
    }

    public void DiscardChanges()
    {
        foreach (var action in _onDiscard)
            action();
    }
}

public sealed class InMemoryVehicleRepository : IVehicleRepository
{
    private readonly Dictionary<Guid, Func<Vehicle>> _factories = new();
    private readonly Dictionary<Guid, Vehicle> _loaded = new();

    public InMemoryVehicleRepository(FakeUnitOfWork unitOfWork)
    {
        unitOfWork.OnDiscard(_loaded.Clear);
    }

    public List<Vehicle> Added { get; } = new();

    /// <summary>Her "yeniden yükleme"de fabrika çağrılır (değişiklik izleyicisinin temizlenmesini taklit eder).</summary>
    public void Store(Guid id, Func<Vehicle> factory) => _factories[id] = factory;

    public Vehicle Store(Vehicle vehicle)
    {
        _factories[vehicle.Id] = () => vehicle;
        return vehicle;
    }

    public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Load(id));

    public Task<IReadOnlyList<Vehicle>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Vehicle>>(ids.Select(Load).OfType<Vehicle>().ToList());

    public Task<IReadOnlySet<Guid>> GetExistingIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlySet<Guid>>(ids.Where(_factories.ContainsKey).ToHashSet());

    public Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
    {
        Added.Add(vehicle);
        return Task.CompletedTask;
    }

    public Task<bool> HasActiveReservationAsync(Guid driverId, CancellationToken cancellationToken = default)
        => Task.FromResult(_factories.Keys.Select(Load).Any(v => v!.Status == VehicleStatus.Reserved && v.ReservedBy == driverId));

    private Vehicle? Load(Guid id)
    {
        if (_loaded.TryGetValue(id, out var vehicle))
            return vehicle;

        if (!_factories.TryGetValue(id, out var factory))
            return null;

        vehicle = factory();
        _loaded[id] = vehicle;
        return vehicle;
    }
}

public sealed class InMemoryRideRepository : IRideRepository
{
    private readonly Dictionary<Guid, Ride> _rides = new();

    public List<Ride> Added { get; } = new();

    public Ride Store(Ride ride)
    {
        _rides[ride.Id] = ride;
        return ride;
    }

    public Task<Ride?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_rides.GetValueOrDefault(id));

    public Task AddAsync(Ride ride, CancellationToken cancellationToken = default)
    {
        Added.Add(ride);
        return Task.CompletedTask;
    }

    public Task<bool> HasActiveRideAsync(Guid driverId, CancellationToken cancellationToken = default)
        => Task.FromResult(_rides.Values.Any(r => r.DriverId == driverId && r.Status == RideStatus.Active));
}

public sealed class InMemoryTelemetryRepository : ITelemetryRepository
{
    public List<TelemetryReading> Readings { get; } = new();

    public void AddRange(IEnumerable<TelemetryReading> readings) => Readings.AddRange(readings);
}

public sealed class InMemoryFieldTaskRepository : IFieldTaskRepository
{
    private readonly Dictionary<Guid, FieldTask> _tasks = new();

    public List<FieldTask> Added { get; } = new();

    public FieldTask Store(FieldTask fieldTask)
    {
        _tasks[fieldTask.Id] = fieldTask;
        return fieldTask;
    }

    public Task<FieldTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_tasks.GetValueOrDefault(id));

    public Task<bool> HasOpenTaskAsync(Guid vehicleId, FieldTaskType type, CancellationToken cancellationToken = default)
        => Task.FromResult(_tasks.Values.Any(t => t.VehicleId == vehicleId && t.Type == type && t.Status != FieldTaskStatus.Completed)
            || Added.Any(t => t.VehicleId == vehicleId && t.Type == type && t.Status != FieldTaskStatus.Completed));

    public Task AddAsync(FieldTask fieldTask, CancellationToken cancellationToken = default)
    {
        Added.Add(fieldTask);
        return Task.CompletedTask;
    }
}

public sealed class StubPaymentGateway : IPaymentGateway
{
    public PaymentGatewayOutcome Outcome { get; set; } = PaymentGatewayOutcome.Approved;

    public List<PaymentAuthorizationRequest> Requests { get; } = new();

    public Task<PaymentGatewayResult> AuthorizeAsync(PaymentAuthorizationRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(new PaymentGatewayResult(Outcome, Outcome.ToString()));
    }
}

public static class Build
{
    public static Vehicle AvailableVehicle(int battery = 80) => new(
        VehicleId.New(),
        new VehicleModel("Xiaomi", 25),
        new GeoPoint(41.0, 29.0),
        new BatteryLevel(battery),
        TestClock.Now);

    public static Vehicle ReservedVehicle(Guid driverId)
    {
        var vehicle = AvailableVehicle();
        vehicle.Reserve(driverId, TestClock.Now);
        return vehicle;
    }

    public static (Vehicle Vehicle, Ride Ride) ActiveRide(Guid driverId, DateTime? startedAt = null)
    {
        var start = startedAt ?? TestClock.Now.AddMinutes(-10);
        var vehicle = AvailableVehicle();
        vehicle.Reserve(driverId, start);
        vehicle.StartRide(driverId, start);

        var ride = new Ride(RideId.New(), driverId, vehicle.Id, vehicle.Location, start);
        return (vehicle, ride);
    }
}
