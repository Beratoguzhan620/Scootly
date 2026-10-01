using Scootly.Application.Riding.Commands;
using Scootly.Domain.Common;
using Scootly.Domain.FieldOps;
using Scootly.Domain.Fleet;
using Scootly.Domain.Riding;
using Xunit;

namespace Scootly.Application.UnitTests;

public sealed class StartRideCommandHandlerTests
{
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly InMemoryVehicleRepository _vehicles;
    private readonly InMemoryRideRepository _rides = new();
    private readonly StartRideCommandHandler _handler;

    public StartRideCommandHandlerTests()
    {
        _vehicles = new InMemoryVehicleRepository(_unitOfWork);
        _handler = new StartRideCommandHandler(_vehicles, _rides, _unitOfWork, new FakeClock());
    }

    [Fact]
    public async Task Rezerve_Eden_Surucu_Surusu_Baslatabilmeli_Ve_Surus_Kimligi_Donmeli()
    {
        var driverId = Guid.NewGuid();
        var vehicle = _vehicles.Store(Build.ReservedVehicle(driverId));

        var result = await _handler.Handle(new StartRideCommand(vehicle.Id, driverId));

        Assert.True(result.IsSuccess);
        Assert.Equal(VehicleStatus.InRide, vehicle.Status);

        var ride = Assert.Single(_rides.Added);
        Assert.Equal(ride.Id, result.Value);
        Assert.Equal(driverId, ride.DriverId);
    }

    [Fact]
    public async Task Baskasinin_Rezerve_Ettigi_Aracla_Surus_Baslatilamamali()
    {
        var vehicle = _vehicles.Store(Build.ReservedVehicle(Guid.NewGuid()));

        var result = await _handler.Handle(new StartRideCommand(vehicle.Id, Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal(VehicleStatus.Reserved, vehicle.Status);
        Assert.Empty(_rides.Added);
    }

    [Fact]
    public async Task Rezervasyonsuz_Arac_Ile_Surus_Baslatilamamali()
    {
        var vehicle = _vehicles.Store(Build.AvailableVehicle());

        var result = await _handler.Handle(new StartRideCommand(vehicle.Id, Guid.NewGuid()));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Devam_Eden_Surusu_Olan_Surucu_Ikinci_Surusu_Baslatamamali()
    {
        var driverId = Guid.NewGuid();
        var (_, activeRide) = Build.ActiveRide(driverId);
        _rides.Store(activeRide);
        var vehicle = _vehicles.Store(Build.ReservedVehicle(driverId));

        var result = await _handler.Handle(new StartRideCommand(vehicle.Id, driverId));

        Assert.False(result.IsSuccess);
        Assert.Empty(_rides.Added);
    }

    [Fact]
    public async Task Olmayan_Arac_NotFound_Donmeli()
    {
        var result = await _handler.Handle(new StartRideCommand(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }
}

public sealed class CompleteRideCommandHandlerTests
{
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly InMemoryVehicleRepository _vehicles;
    private readonly InMemoryRideRepository _rides = new();
    private readonly CompleteRideCommandHandler _handler;

    public CompleteRideCommandHandlerTests()
    {
        _vehicles = new InMemoryVehicleRepository(_unitOfWork);
        _handler = new CompleteRideCommandHandler(_rides, _vehicles, _unitOfWork, new FakeClock());
    }

    [Fact]
    public async Task Surucu_Kendi_Surusunu_Tamamlayabilmeli_Ve_Ucret_Hesaplanmali()
    {
        var driverId = Guid.NewGuid();
        var (vehicle, ride) = Build.ActiveRide(driverId, TestClock.Now.AddMinutes(-10));
        _vehicles.Store(vehicle);
        _rides.Store(ride);

        var result = await _handler.Handle(new CompleteRideCommand(ride.Id, driverId, 41.01, 29.01));

        Assert.True(result.IsSuccess);
        Assert.Equal(RideStatus.Completed, ride.Status);
        Assert.Equal(VehicleStatus.Available, vehicle.Status);
        Assert.Equal(25.00m, result.Value!.Fare);
        Assert.Equal(PaymentStatus.Pending, result.Value.PaymentStatus);
    }

    [Fact]
    public async Task Baska_Bir_Kullanici_Surusu_Tamamlayamamali_IDOR()
    {
        var owner = Guid.NewGuid();
        var (vehicle, ride) = Build.ActiveRide(owner);
        _vehicles.Store(vehicle);
        _rides.Store(ride);

        var result = await _handler.Handle(new CompleteRideCommand(ride.Id, Guid.NewGuid(), 41.01, 29.01));

        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Equal(RideStatus.Active, ride.Status);
        Assert.Equal(VehicleStatus.InRide, vehicle.Status);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Var_Olmayan_Surus_NotFound_Donmeli()
    {
        var result = await _handler.Handle(new CompleteRideCommand(Guid.NewGuid(), Guid.NewGuid(), 41.01, 29.01));

        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task Tamamlanmis_Surus_Tekrar_Tamamlanamaz()
    {
        var driverId = Guid.NewGuid();
        var (vehicle, ride) = Build.ActiveRide(driverId);
        _vehicles.Store(vehicle);
        _rides.Store(ride);

        await _handler.Handle(new CompleteRideCommand(ride.Id, driverId, 41.01, 29.01));
        var second = await _handler.Handle(new CompleteRideCommand(ride.Id, driverId, 41.01, 29.01));

        Assert.False(second.IsSuccess);
        Assert.Equal(ErrorType.Conflict, second.ErrorType);
    }
}

public sealed class AbandonRideCommandHandlerTests
{
    [Fact]
    public async Task Terk_Edilen_Surus_Kapatilmali_Ve_Arac_Bakima_Alinmali()
    {
        var unitOfWork = new FakeUnitOfWork();
        var vehicles = new InMemoryVehicleRepository(unitOfWork);
        var rides = new InMemoryRideRepository();
        var (vehicle, ride) = Build.ActiveRide(Guid.NewGuid(), TestClock.Now.AddHours(-3));
        vehicles.Store(vehicle);
        rides.Store(ride);

        var fieldTasks = new InMemoryFieldTaskRepository();
        var result = await new AbandonRideCommandHandler(rides, vehicles, fieldTasks, unitOfWork, new FakeClock())
            .Handle(new AbandonRideCommand(ride.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(RideStatus.Abandoned, ride.Status);
        Assert.Equal(PaymentStatus.Pending, ride.PaymentStatus);
        Assert.Equal(VehicleStatus.Maintenance, vehicle.Status);
    }

    [Fact]
    public async Task Terk_Edilen_Surus_Icin_Inspection_Gorevi_Olusturulmali()
    {
        var unitOfWork = new FakeUnitOfWork();
        var vehicles = new InMemoryVehicleRepository(unitOfWork);
        var rides = new InMemoryRideRepository();
        var fieldTasks = new InMemoryFieldTaskRepository();
        var (vehicle, ride) = Build.ActiveRide(Guid.NewGuid(), TestClock.Now.AddHours(-3));
        vehicles.Store(vehicle);
        rides.Store(ride);

        var result = await new AbandonRideCommandHandler(rides, vehicles, fieldTasks, unitOfWork, new FakeClock())
            .Handle(new AbandonRideCommand(ride.Id));

        Assert.True(result.IsSuccess);
        Assert.Single(fieldTasks.Added);
        Assert.Equal(vehicle.Id, fieldTasks.Added[0].VehicleId);
        Assert.Equal(FieldTaskType.Inspection, fieldTasks.Added[0].Type);
    }

    [Fact]
    public async Task Ayni_Arac_Icin_Acik_Inspection_Gorevi_Varsa_Yenisi_Acilmamali()
    {
        var unitOfWork = new FakeUnitOfWork();
        var vehicles = new InMemoryVehicleRepository(unitOfWork);
        var rides = new InMemoryRideRepository();
        var fieldTasks = new InMemoryFieldTaskRepository();
        var (vehicle, ride) = Build.ActiveRide(Guid.NewGuid(), TestClock.Now.AddHours(-3));
        vehicles.Store(vehicle);
        rides.Store(ride);
        fieldTasks.Store(new FieldTask(Guid.NewGuid(), vehicle.Id, FieldTaskType.Inspection, TestClock.Now));

        var result = await new AbandonRideCommandHandler(rides, vehicles, fieldTasks, unitOfWork, new FakeClock())
            .Handle(new AbandonRideCommand(ride.Id));

        Assert.True(result.IsSuccess);
        Assert.Empty(fieldTasks.Added);
    }
}