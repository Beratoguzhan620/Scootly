using Scootly.Domain.FieldOps;
using Xunit;

namespace Scootly.Domain.UnitTests;

public class FieldTaskPhotoKeyTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    private static FieldTask AssignedTask(Guid operatorId)
    {
        var task = new FieldTask(Guid.NewGuid(), Guid.NewGuid(), FieldTaskType.Inspection, Now);
        task.Assign(operatorId, Now);
        return task;
    }

    [Fact]
    public void Complete_Fotograf_Anahtarini_Saklar()
    {
        var operatorId = Guid.NewGuid();
        var task = AssignedTask(operatorId);

        task.Complete(operatorId, Now.AddMinutes(5), "Tamam", "field-tasks/a/b.jpg");

        Assert.Equal("field-tasks/a/b.jpg", task.PhotoObjectKey);
    }

    [Fact]
    public void Complete_Anahtarsiz_Cagrida_Anahtar_Bos_Kalir()
    {
        var operatorId = Guid.NewGuid();
        var task = AssignedTask(operatorId);

        task.Complete(operatorId, Now.AddMinutes(5), "Tamam");

        Assert.Null(task.PhotoObjectKey);
    }
}
