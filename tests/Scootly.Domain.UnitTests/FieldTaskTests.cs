using Scootly.Domain.Common;
using Scootly.Domain.FieldOps;
using Xunit;

namespace Scootly.Domain.UnitTests;

public class FieldTaskTests
{
    [Fact]
    public void Dusuk_Batarya_Gorevi_Acik_Olarak_Olusur()
    {
        var aracId = Guid.NewGuid();
        var simdi = new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

        var gorev = FieldTask.ForLowBattery(aracId, 12, simdi);

        Assert.Equal(aracId, gorev.VehicleId);
        Assert.Equal(FieldTaskType.BatteryReplacement, gorev.Type);
        Assert.Equal(FieldTaskStatus.Open, gorev.Status);
        Assert.Equal(simdi, gorev.CreatedAt);
        Assert.Contains("12", gorev.Reason, StringComparison.Ordinal);
        Assert.True(gorev.Reason.Length <= FieldTask.MaxReasonLength);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Gecersiz_Batarya_Yuzdesi_Reddedilir(int yuzde)
    {
        Assert.Throws<DomainException>(() => FieldTask.ForLowBattery(Guid.NewGuid(), yuzde, DateTime.UtcNow));
    }

    [Fact]
    public void Arac_Kimligi_Bos_Olamaz()
    {
        Assert.Throws<DomainException>(() => FieldTask.ForLowBattery(Guid.Empty, 10, DateTime.UtcNow));
    }

    [Fact]
    public void Tamamlanan_Gorev_Ikinci_Kez_Tamamlanamaz()
    {
        var gorev = FieldTask.ForLowBattery(Guid.NewGuid(), 10, DateTime.UtcNow);
        gorev.Complete(DateTime.UtcNow);

        Assert.Equal(FieldTaskStatus.Completed, gorev.Status);
        Assert.NotNull(gorev.CompletedAt);
        Assert.Throws<DomainException>(() => gorev.Complete(DateTime.UtcNow));
    }
}
