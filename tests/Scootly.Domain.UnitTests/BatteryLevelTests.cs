using System;
using System.Collections.Generic;
using System.Text;

using Scootly.Domain.Common;
using Scootly.Domain.Fleet;
using Xunit;

namespace Scootly.Domain.UnitTests;

public class BatteryLevelTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void Gecerli_Yuzde_Kabul_Edilmeli(int yuzde)
    {
        var batarya = new BatteryLevel(yuzde);

        Assert.Equal(yuzde, batarya.Percentage);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(150)]
    public void Gecersiz_Yuzde_DomainException_Firlatmali(int yuzde)
    {
        Assert.Throws<DomainException>(() => new BatteryLevel(yuzde));
    }

    [Fact]
    public void Ayni_Yuzdeye_Sahip_Iki_Batarya_Esit_Olmali()
    {
        Assert.True(new BatteryLevel(80).Equals(new BatteryLevel(80)));
    }

    [Fact]
    public void Farkli_Yuzdeye_Sahip_Iki_Batarya_Esit_Olmamali()
    {
        Assert.False(new BatteryLevel(80).Equals(new BatteryLevel(20)));
    }
}
