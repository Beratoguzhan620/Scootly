using Scootly.Application.FieldOps.Commands;
using Scootly.Domain.FieldOps;
using Xunit;

namespace Scootly.Application.UnitTests;

public class FieldTaskHandlerTests
{
    [Fact]
    public async Task Acik_Gorev_Yoksa_Yeni_Gorev_Olusturulmali()
    {
        var unitOfWork = new FakeUnitOfWork();
        var fieldTasks = new InMemoryFieldTaskRepository();
        var handler = new FieldTaskCommandHandler(fieldTasks, unitOfWork, new FakeClock(), new FakeFileStorage());

        var vehicleId = Guid.NewGuid();
        var result = await handler.Handle(new CreateFieldTaskCommand(vehicleId, FieldTaskType.BatteryReplacement));

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);
        Assert.Single(fieldTasks.Added);
        Assert.Equal(vehicleId, fieldTasks.Added[0].VehicleId);
    }

    [Fact]
    public async Task Ayni_Arac_Ve_Tur_Icin_Acik_Gorev_Varsa_Yenisi_Acilmamali()
    {
        var unitOfWork = new FakeUnitOfWork();
        var fieldTasks = new InMemoryFieldTaskRepository();
        var vehicleId = Guid.NewGuid();

        fieldTasks.Store(new FieldTask(Guid.NewGuid(), vehicleId, FieldTaskType.BatteryReplacement, TestClock.Now));

        var handler = new FieldTaskCommandHandler(fieldTasks, unitOfWork, new FakeClock(), new FakeFileStorage());
        var result = await handler.Handle(new CreateFieldTaskCommand(vehicleId, FieldTaskType.BatteryReplacement));

        Assert.True(result.IsSuccess);
        Assert.Empty(fieldTasks.Added);
    }

    [Fact]
    public async Task Gorev_Atama_Basarili_Olmali()
    {
        var unitOfWork = new FakeUnitOfWork();
        var fieldTasks = new InMemoryFieldTaskRepository();
        var task = fieldTasks.Store(new FieldTask(Guid.NewGuid(), Guid.NewGuid(), FieldTaskType.Inspection, TestClock.Now));

        var handler = new FieldTaskCommandHandler(fieldTasks, unitOfWork, new FakeClock(), new FakeFileStorage());
        var operatorId = Guid.NewGuid();
        var result = await handler.Handle(new AssignFieldTaskCommand(task.Id, operatorId));

        Assert.True(result.IsSuccess);
        Assert.Equal(FieldTaskStatus.Assigned, task.Status);
        Assert.Equal(operatorId, task.AssignedTo);
    }

    [Fact]
    public async Task Gorev_Tamamlama_Basarili_Olmali()
    {
        var unitOfWork = new FakeUnitOfWork();
        var fieldTasks = new InMemoryFieldTaskRepository();
        var operatorId = Guid.NewGuid();
        var task = fieldTasks.Store(new FieldTask(Guid.NewGuid(), Guid.NewGuid(), FieldTaskType.Inspection, TestClock.Now));
        task.Assign(operatorId, TestClock.Now);

        var handler = new FieldTaskCommandHandler(fieldTasks, unitOfWork, new FakeClock(), new FakeFileStorage());
        var result = await handler.Handle(new CompleteFieldTaskCommand(task.Id, operatorId, "Kontrol edildi."));

        Assert.True(result.IsSuccess);
        Assert.Equal(FieldTaskStatus.Completed, task.Status);
    }

    [Fact]
    public async Task Olmayan_Gorev_Icin_NotFound_Donmeli()
    {
        var unitOfWork = new FakeUnitOfWork();
        var fieldTasks = new InMemoryFieldTaskRepository();
        var handler = new FieldTaskCommandHandler(fieldTasks, unitOfWork, new FakeClock(), new FakeFileStorage());

        var result = await handler.Handle(new AssignFieldTaskCommand(Guid.NewGuid(), Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(Domain.Common.ErrorType.NotFound, result.ErrorType);
    }
}