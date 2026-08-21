using Scootly.Domain.Common;
using Xunit;

namespace Scootly.Domain.UnitTests;

public class ResultTests
{
    [Fact]
    public void Success_IsSuccess_True_Donmeli()
    {
        var sonuc = Result.Success();

        Assert.True(sonuc.IsSuccess);
        Assert.Equal(string.Empty, sonuc.Error);
    }

    [Fact]
    public void Failure_IsSuccess_False_Ve_HataMesaji_Tasimali()
    {
        var sonuc = Result.Failure("bir şeyler ters gitti");

        Assert.False(sonuc.IsSuccess);
        Assert.Equal("bir şeyler ters gitti", sonuc.Error);
    }
}