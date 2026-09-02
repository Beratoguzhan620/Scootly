using Scootly.Domain.Common;
using Scootly.Domain.Fleet;
using Xunit;

namespace Scootly.Domain.UnitTests;

public class BatteryLevelTests
{
    [Fact]
    public void Negatif_Yuzde_Ile_BatteryLevel_Olusturulamaz()
    {
        Assert.Throws<DomainException>(() => new BatteryLevel(-1));
    }

    [Fact]
    public void Yuzyuzden_Buyuk_Yuzde_Ile_BatteryLevel_Olusturulamaz()
    {
        Assert.Throws<DomainException>(() => new BatteryLevel(101));
    }
}