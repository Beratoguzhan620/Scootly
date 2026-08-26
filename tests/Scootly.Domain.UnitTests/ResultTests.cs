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

    [Fact]
    public void Generic_Success_Degeri_Tasimali()
    {
        var sonuc = Result<int>.Success(42);

        Assert.True(sonuc.IsSuccess);
        Assert.Equal(42, sonuc.Value);
    }

    [Fact]
    public void Generic_Failure_Deger_Tasimamali()
    {
        var sonuc = Result<int>.Failure("olmadı");

        Assert.False(sonuc.IsSuccess);
        Assert.Equal("olmadı", sonuc.Error);
    }
}