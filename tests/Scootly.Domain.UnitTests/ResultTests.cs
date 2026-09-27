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
        Assert.Equal(ErrorType.None, sonuc.ErrorType);
    }

    [Fact]
    public void Failure_IsSuccess_False_Ve_HataMesaji_Tasimali()
    {
        var sonuc = Result.Failure("bir şeyler ters gitti");

        Assert.False(sonuc.IsSuccess);
        Assert.Equal("bir şeyler ters gitti", sonuc.Error);
        Assert.Equal(ErrorType.Conflict, sonuc.ErrorType);
    }

    [Fact]
    public void Hata_Turleri_Dogru_Tasinmali()
    {
        Assert.Equal(ErrorType.NotFound, Result.NotFound("yok").ErrorType);
        Assert.Equal(ErrorType.Validation, Result.Validation("geçersiz").ErrorType);
        Assert.Equal(ErrorType.Forbidden, Result.Forbidden("yasak").ErrorType);
        Assert.Equal(ErrorType.NotFound, Result<int>.NotFound("yok").ErrorType);
    }

    [Fact]
    public void Generic_Success_Degeri_Tasimali()
    {
        var sonuc = Result<int>.Success(42);

        Assert.True(sonuc.IsSuccess);
        Assert.Equal(42, sonuc.Value);
    }
}
