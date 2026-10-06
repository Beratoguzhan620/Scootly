using Scootly.Infrastructure.Storage;
using Xunit;

namespace Scootly.Infrastructure.Tests;

public class StorageUnitTests
{
    private static StorageOptions ValidOptions() => new()
    {
        Enabled = true,
        Endpoint = "http://seaweedfs:8333",
        PublicEndpoint = "http://127.0.0.1:8333",
        Bucket = "scootly-field-photos",
        AccessKey = "testaccess",
        SecretKey = "testsecret-0123456789abcdef"
    };

    [Fact]
    public void Kapaliyken_Bos_Ayarlar_Gecerlidir()
    {
        Assert.True(StorageOptions.IsValid(new StorageOptions()));
    }

    [Fact]
    public void Acikken_Tam_Ayarlar_Gecerlidir()
    {
        Assert.True(StorageOptions.IsValid(ValidOptions()));
    }

    [Fact]
    public void Acikken_Eksik_Anahtar_Gecersizdir()
    {
        var options = ValidOptions();
        options.AccessKey = string.Empty;

        Assert.False(StorageOptions.IsValid(options));
    }

    [Fact]
    public void Acikken_Kisa_Gizli_Anahtar_Gecersizdir()
    {
        var options = ValidOptions();
        options.SecretKey = "kisa";

        Assert.False(StorageOptions.IsValid(options));
    }

    [Theory]
    [InlineData("AB")]
    [InlineData("Buyuk-Harf")]
    [InlineData("-baslangic")]
    [InlineData("bitis-")]
    public void Acikken_Gecersiz_Kova_Adi_Reddedilir(string bucket)
    {
        var options = ValidOptions();
        options.Bucket = bucket;

        Assert.False(StorageOptions.IsValid(options));
    }

    [Theory]
    [InlineData("")]
    [InlineData("seaweedfs:8333")]
    [InlineData("ftp://host/x")]
    public void Acikken_Gecersiz_Adres_Reddedilir(string endpoint)
    {
        var options = ValidOptions();
        options.PublicEndpoint = endpoint;

        Assert.False(StorageOptions.IsValid(options));
    }

    [Fact]
    public void On_Imzali_Url_Genel_Adrese_Gore_Imzalanir_Ve_Sir_Icermez()
    {
        using var storage = new S3FileStorage(ValidOptions());

        var url = storage.CreateDownloadUrl("field-tasks/a/b.jpg", TimeSpan.FromSeconds(60));
        var uri = new Uri(url);

        Assert.Equal("127.0.0.1", uri.Host);
        Assert.Equal(8333, uri.Port);
        Assert.Equal("/scootly-field-photos/field-tasks/a/b.jpg", uri.AbsolutePath);
        Assert.Contains("X-Amz-Signature=", url);
        Assert.Contains("X-Amz-Expires=60", url);
        Assert.DoesNotContain("testsecret", url);
    }

    [Fact]
    public void Kapali_Depo_Islem_Yapmaz()
    {
        var storage = new DisabledFileStorage();

        Assert.False(storage.IsEnabled);
        Assert.Throws<InvalidOperationException>(() => storage.CreateDownloadUrl("x", TimeSpan.FromSeconds(60)));
    }
}
