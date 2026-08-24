using Scootly.Domain.Common;
using Xunit;

namespace Scootly.Domain.UnitTests;

// Test amaçlı sahte bir değer nesnesi
public class TestMoney : ValueObject
{
    public decimal Amount { get; }

    public TestMoney(decimal amount)
    {
        Amount = amount;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
    }
}

public class ValueObjectTests
{
    [Fact]
    public void Ayni_Degere_Sahip_Iki_ValueObject_Esit_Olmali()
    {
        var para1 = new TestMoney(100);
        var para2 = new TestMoney(100);

        Assert.True(para1.Equals(para2));
    }

    [Fact]
    public void Farkli_Degere_Sahip_Iki_ValueObject_Esit_Olmamali()
    {
        var para1 = new TestMoney(100);
        var para2 = new TestMoney(50);

        Assert.False(para1.Equals(para2));
    }
}