using Scootly.Application.Common;
using Scootly.Application.Riding.Commands;
using Scootly.Domain.Common;
using Scootly.Domain.Fleet;
using Xunit;

namespace Scootly.Application.UnitTests;

public sealed class ReserveVehicleCommandHandlerTests
{
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly InMemoryVehicleRepository _vehicles;
    private readonly InMemoryRideRepository _rides = new();
    private readonly ReserveVehicleCommandHandler _handler;
    private readonly Guid _driverId = Guid.NewGuid();

    public ReserveVehicleCommandHandlerTests()
    {
        _vehicles = new InMemoryVehicleRepository(_unitOfWork);
        _handler = new ReserveVehicleCommandHandler(_vehicles, _rides, _unitOfWork, new FakeClock());
    }

    [Fact]
    public async Task Musait_Arac_Surucu_Adina_Rezerve_Edilmeli()
    {
        var vehicle = _vehicles.Store(Build.AvailableVehicle());

        var result = await _handler.Handle(new ReserveVehicleCommand(vehicle.Id, _driverId));

        Assert.True(result.IsSuccess);
        Assert.Equal(VehicleStatus.Reserved, vehicle.Status);
        Assert.Equal(_driverId, vehicle.ReservedBy);
        Assert.Equal(1, _unitOfWork.SuccessfulSaveCount);
    }

    [Fact]
    public async Task Olmayan_Arac_NotFound_Donmeli()
    {
        var result = await _handler.Handle(new ReserveVehicleCommand(Guid.NewGuid(), _driverId));

        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task Ikinci_Aktif_Rezervasyona_Izin_Verilmemeli()
    {
        _vehicles.Store(Build.ReservedVehicle(_driverId));
        var second = _vehicles.Store(Build.AvailableVehicle());

        var result = await _handler.Handle(new ReserveVehicleCommand(second.Id, _driverId));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal(VehicleStatus.Available, second.Status);
    }

    [Fact]
    public async Task Aktif_Surus_Varken_Rezervasyon_Yapilamamali()
    {
        var (_, ride) = Build.ActiveRide(_driverId);
        _rides.Store(ride);
        var vehicle = _vehicles.Store(Build.AvailableVehicle());

        var result = await _handler.Handle(new ReserveVehicleCommand(vehicle.Id, _driverId));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Eszamanlilik_Cakismasinda_Taze_Veriyle_Yeniden_Denemeli()
    {
        var vehicleId = Guid.NewGuid();
        _vehicles.Store(vehicleId, () => new Vehicle(
            new VehicleId(vehicleId), new Domain.Fleet.VehicleModel("Xiaomi", 25),
            new Domain.Geo.GeoPoint(41, 29), new BatteryLevel(80), TestClock.Now));
        _unitOfWork.ConcurrencyConflictsToThrow = 1;

        var result = await _handler.Handle(new ReserveVehicleCommand(vehicleId, _driverId));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Surekli_Cakismada_Anlasilir_Hata_Donmeli()
    {
        var vehicleId = Guid.NewGuid();
        _vehicles.Store(vehicleId, () => new Vehicle(
            new VehicleId(vehicleId), new Domain.Fleet.VehicleModel("Xiaomi", 25),
            new Domain.Geo.GeoPoint(41, 29), new BatteryLevel(80), TestClock.Now));
        _unitOfWork.ConcurrencyConflictsToThrow = OptimisticConcurrency.MaxAttempts;

        var result = await _handler.Handle(new ReserveVehicleCommand(vehicleId, _driverId));

        Assert.False(result.IsSuccess);
        Assert.Contains("başka biri tarafından rezerve edildi", result.Error);
        Assert.Equal(OptimisticConcurrency.MaxAttempts, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Veritabani_Benzersizlik_Ihlali_Kullanici_Mesajina_Cevrilmeli()
    {
        var vehicle = _vehicles.Store(Build.AvailableVehicle());
        _unitOfWork.UniqueViolationToThrow = ConstraintNames.OneActiveReservationPerDriver;

        var result = await _handler.Handle(new ReserveVehicleCommand(vehicle.Id, _driverId));

        Assert.False(result.IsSuccess);
        Assert.Equal("Zaten aktif bir rezervasyonunuz var.", result.Error);
    }
}

public sealed class CancelReservationCommandHandlerTests
{
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly InMemoryVehicleRepository _vehicles;
    private readonly CancelReservationCommandHandler _handler;

    public CancelReservationCommandHandlerTests()
    {
        _vehicles = new InMemoryVehicleRepository(_unitOfWork);
        _handler = new CancelReservationCommandHandler(_vehicles, _unitOfWork, new FakeClock());
    }

    [Fact]
    public async Task Sahibi_Rezervasyonunu_Iptal_Edebilmeli()
    {
        var driverId = Guid.NewGuid();
        var vehicle = _vehicles.Store(Build.ReservedVehicle(driverId));

        var result = await _handler.Handle(new CancelReservationCommand(vehicle.Id, driverId));

        Assert.True(result.IsSuccess);
        Assert.Equal(VehicleStatus.Available, vehicle.Status);
    }

    [Fact]
    public async Task Baskasinin_Rezervasyonu_Iptal_Edilemez_Ve_Varligi_Aciga_Cikmaz()
    {
        var vehicle = _vehicles.Store(Build.ReservedVehicle(Guid.NewGuid()));

        var result = await _handler.Handle(new CancelReservationCommand(vehicle.Id, Guid.NewGuid()));

        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Equal(VehicleStatus.Reserved, vehicle.Status);
    }
}

public sealed class ExpireReservationCommandHandlerTests
{
    [Fact]
    public async Task Suresi_Dolan_Rezervasyon_Kaldirilmali()
    {
        var unitOfWork = new FakeUnitOfWork();
        var vehicles = new InMemoryVehicleRepository(unitOfWork);
        var vehicle = vehicles.Store(Build.ReservedVehicle(Guid.NewGuid()));
        var clock = new FakeClock { UtcNow = TestClock.Now.AddMinutes(11) };

        var result = await new ExpireReservationCommandHandler(vehicles, unitOfWork, clock).Handle(new ExpireReservationCommand(vehicle.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(VehicleStatus.Available, vehicle.Status);
    }

    [Fact]
    public async Task Suresi_Dolmamis_Rezervasyon_Korunmali()
    {
        var unitOfWork = new FakeUnitOfWork();
        var vehicles = new InMemoryVehicleRepository(unitOfWork);
        var vehicle = vehicles.Store(Build.ReservedVehicle(Guid.NewGuid()));
        var clock = new FakeClock { UtcNow = TestClock.Now.AddMinutes(3) };

        var result = await new ExpireReservationCommandHandler(vehicles, unitOfWork, clock).Handle(new ExpireReservationCommand(vehicle.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal(VehicleStatus.Reserved, vehicle.Status);
        Assert.Equal(0, unitOfWork.SaveCount);
    }
}
