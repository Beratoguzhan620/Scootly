using Scootly.Domain.Common;
using Scootly.Domain.FieldOps;
using Scootly.Domain.FieldOps.Events;
using Xunit;

namespace Scootly.Domain.UnitTests;

public class FieldTaskTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly Guid OperatorA = Guid.NewGuid();
    private static readonly Guid OperatorB = Guid.NewGuid();

    private static FieldTask CreateOpenTask() =>
        new(Guid.NewGuid(), VehicleId, FieldTaskType.BatteryReplacement, Now);

    [Fact]
    public void Olusturulan_Gorev_Open_Durumunda_Baslamali()
    {
        var task = CreateOpenTask();

        Assert.Equal(FieldTaskStatus.Open, task.Status);
        Assert.Null(task.AssignedTo);
        Assert.Equal(Now, task.CreatedAt);
    }

    [Fact]
    public void Bos_Arac_Kimligiyle_Gorev_Olusturulamamali()
    {
        Assert.Throws<DomainException>(() => new FieldTask(Guid.NewGuid(), Guid.Empty, FieldTaskType.BatteryReplacement, Now));
    }

    [Fact]
    public void Open_Gorev_Assign_Cagrilinca_Operatore_Atanmali()
    {
        var task = CreateOpenTask();

        task.Assign(OperatorA, Now);

        Assert.Equal(FieldTaskStatus.Assigned, task.Status);
        Assert.Equal(OperatorA, task.AssignedTo);
        Assert.Equal(Now, task.AssignedAt);
    }

    [Fact]
    public void Zaten_Atanmis_Gorev_Tekrar_Assign_Edilemez()
    {
        var task = CreateOpenTask();
        task.Assign(OperatorA, Now);

        Assert.Throws<DomainException>(() => task.Assign(OperatorB, Now));
    }

    [Fact]
    public void Atanan_Operator_Gorevi_Tamamlayabilmeli()
    {
        var task = CreateOpenTask();
        task.Assign(OperatorA, Now);

        task.Complete(OperatorA, Now.AddMinutes(30), "Batarya değiştirildi.");

        Assert.Equal(FieldTaskStatus.Completed, task.Status);
        Assert.Equal(Now.AddMinutes(30), task.CompletedAt);
        Assert.Equal("Batarya değiştirildi.", task.Note);
    }

    [Fact]
    public void Atanmayan_Operator_Gorevi_Tamamlayamaz()
    {
        var task = CreateOpenTask();
        task.Assign(OperatorA, Now);

        var ex = Assert.Throws<DomainException>(() => task.Complete(OperatorB, Now, null));

        Assert.Contains("size atanmamış", ex.Message);
        Assert.Equal(FieldTaskStatus.Assigned, task.Status);
    }

    [Fact]
    public void Open_Gorev_Dogrudan_Tamamlanamaz()
    {
        var task = CreateOpenTask();

        Assert.Throws<DomainException>(() => task.Complete(OperatorA, Now, null));
    }

    [Fact]
    public void Olusturma_Olayi_Dogru_Bilgiyle_Uretilmeli()
    {
        var task = CreateOpenTask();

        var created = Assert.Single(task.DomainEvents.OfType<FieldTaskCreatedEvent>());
        Assert.Equal(VehicleId, created.VehicleId);
        Assert.Equal(FieldTaskType.BatteryReplacement, created.Type);
    }
}
