using Scootly.Application.FieldOps;
using Scootly.Domain.Common;
using Xunit;

namespace Scootly.Application.UnitTests;

public class FieldTaskPhotoRulesTests
{
    private static byte[] JpegOfLength(int length)
    {
        var bytes = new byte[length];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        return bytes;
    }

    [Fact]
    public void Jpeg_Imzasi_Kabul_Edilir()
    {
        var result = FieldTaskPhotoRules.Inspect(JpegOfLength(16));

        Assert.True(result.IsSuccess);
        Assert.Equal("image/jpeg", result.Value!.ContentType);
        Assert.Equal(".jpg", result.Value.Extension);
    }

    [Fact]
    public void Png_Imzasi_Kabul_Edilir()
    {
        var result = FieldTaskPhotoRules.Inspect(Convert.FromHexString("89504E470D0A1A0A0000000D"));

        Assert.True(result.IsSuccess);
        Assert.Equal("image/png", result.Value!.ContentType);
        Assert.Equal(".png", result.Value.Extension);
    }

    [Fact]
    public void Bos_Icerik_Reddedilir()
    {
        var result = FieldTaskPhotoRules.Inspect([]);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public void Sinirdaki_Icerik_Kabul_Edilir()
    {
        Assert.True(FieldTaskPhotoRules.Inspect(JpegOfLength(FieldTaskPhotoRules.MaxBytes)).IsSuccess);
    }

    [Fact]
    public void Siniri_Asan_Icerik_Reddedilir()
    {
        var result = FieldTaskPhotoRules.Inspect(JpegOfLength(FieldTaskPhotoRules.MaxBytes + 1));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Theory]
    [InlineData("4D5A900003000000")]
    [InlineData("474946383961")]
    [InlineData("255044462D")]
    [InlineData("3C68746D6C3E")]
    public void Taninmayan_Imzalar_Reddedilir(string hex)
    {
        var result = FieldTaskPhotoRules.Inspect(Convert.FromHexString(hex));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }
}
