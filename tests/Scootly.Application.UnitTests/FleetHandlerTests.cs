using Scootly.Application.Fleet.Commands;
using Scootly.Domain.Common;
using Scootly.Domain.Fleet;
using Xunit;

namespace Scootly.Application.UnitTests;

public sealed class RegisterVehicleCommandHandlerTests
{
    [Fact]
    public async Task Gecerli_Arac_Kaydedilmeli_Ve_Kimligi_Donmeli()
    {
        var unitOfWork = new FakeUnitOfWork();
        var vehicles = new InMemoryVehicleRepository(unitOfWork);
        var handler = new RegisterVehicleCommandHandler(vehicles, unitOfWork, new FakeClock());

        var result = await handler.Handle(new RegisterVehicleCommand("Segway", 30, 41.0, 29.0, 90));

        Assert.True(result.IsSuccess);
        var added = Assert.Single(vehicles.Added);
        Assert.Equal(added.Id, result.Value);
        Assert.Equal(1, unitOfWork.SuccessfulSaveCount);
    }

    [Fact]
    public async Task Gecersiz_Arac_Dogrulama_Hatasi_Donmeli_Ve_Kaydedilmemeli()
    {
        var unitOfWork = new FakeUnitOfWork();
        var vehicles = new InMemoryVehicleRepository(unitOfWork);
        var handler = new RegisterVehicleCommandHandler(vehicles, unitOfWork, new FakeClock());

        var result = await handler.Handle(new RegisterVehicleCommand("", 30, 41.0, 29.0, 150));

        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Empty(vehicles.Added);
        Assert.Equal(0, unitOfWork.SaveCount);
    }
}

public sealed class VehicleMaintenanceCommandHandlerTests
{
    [Fact]
    public async Task Surusteki_Arac_Bakima_Alinamamali()
    {
        var unitOfWork = new FakeUnitOfWork();
        var vehicles = new InMemoryVehicleRepository(unitOfWork);
        var (vehicle, _) = Build.ActiveRide(Guid.NewGuid());
        vehicles.Store(vehicle);

        var result = await new VehicleMaintenanceCommandHandler(vehicles, unitOfWork, new FakeClock())
            .Handle(new SendVehicleToMaintenanceCommand(vehicle.Id));

        Assert.Equal(ErrorType.Conflict, result.ErrorType);
        Assert.Equal(VehicleStatus.InRide, vehicle.Status);
    }

    [Fact]
    public async Task Bakimdaki_Arac_Hizmete_Donebilmeli()
    {
        var unitOfWork = new FakeUnitOfWork();
        var vehicles = new InMemoryVehicleRepository(unitOfWork);
        var vehicle = vehicles.Store(Build.AvailableVehicle());
        var handler = new VehicleMaintenanceCommandHandler(vehicles, unitOfWork, new FakeClock());

        await handler.Handle(new SendVehicleToMaintenanceCommand(vehicle.Id));
        var result = await handler.Handle(new ReturnVehicleToServiceCommand(vehicle.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(VehicleStatus.Available, vehicle.Status);
    }
}
