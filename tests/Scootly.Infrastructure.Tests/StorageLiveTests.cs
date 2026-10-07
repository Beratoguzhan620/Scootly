using System.Net;
using Scootly.Infrastructure.Storage;
using Xunit;

namespace Scootly.Infrastructure.Tests;

/// <summary>
/// Canlı S3 uyumlu depoya karşı ölçüm. SCOOTLY_LIVE_STORAGE_ENDPOINT / _ACCESS_KEY / _SECRET_KEY tanımlı değilse atlanır.
/// SCOOTLY_LIVE_STORAGE_REPORT tanimliysa gozlemler bu dosyaya yazilir (gizli bilgi icermez).
/// </summary>
public class StorageLiveTests
{
    private static StorageOptions? LiveOptions()
    {
        var endpoint = Environment.GetEnvironmentVariable("SCOOTLY_LIVE_STORAGE_ENDPOINT");
        var access = Environment.GetEnvironmentVariable("SCOOTLY_LIVE_STORAGE_ACCESS_KEY");
        var secret = Environment.GetEnvironmentVariable("SCOOTLY_LIVE_STORAGE_SECRET_KEY");

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(access) || string.IsNullOrWhiteSpace(secret))
            return null;

        return new StorageOptions
        {
            Enabled = true,
            Endpoint = endpoint,
            PublicEndpoint = endpoint,
            Bucket = "scootly-live-test",
            AccessKey = access,
            SecretKey = secret
        };
    }

    [Fact]
    public async Task Canli_Depo_Yukleme_Imzali_Indirme_Sure_Sonu_Ve_Silme()
    {
        var options = LiveOptions();
        Assert.SkipUnless(options is not null, "Canlı depo ortam değişkenleri tanımlı değil.");

        using var storage = new S3FileStorage(options!);
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };

        var key = $"live-test/{Guid.NewGuid():N}.jpg";
        var content = new byte[2048];
        new Random(42).NextBytes(content);
        content[0] = 0xFF;
        content[1] = 0xD8;
        content[2] = 0xFF;

        var observed = new List<string>();
        var failures = new List<string>();

        void Check(string name, HttpStatusCode actual, HttpStatusCode expected)
        {
            observed.Add($"{name}: {(int)actual} {actual} (beklenen {(int)expected})");

            if (actual != expected)
                failures.Add(name);
        }

        await storage.PutAsync(key, content, "image/jpeg");
        observed.Add("yukleme: tamam");

        var url = storage.CreateDownloadUrl(key, TimeSpan.FromSeconds(60));

        using (var ok = await http.GetAsync(url))
        {
            Check("geçerli imzalı url", ok.StatusCode, HttpStatusCode.OK);

            var body = await ok.Content.ReadAsByteArrayAsync();
            var sameBody = body.AsSpan().SequenceEqual(content);
            var mediaType = ok.Content.Headers.ContentType?.MediaType;
            observed.Add($"gövde aynı: {sameBody}; content-type: {mediaType}");

            if (!sameBody || mediaType != "image/jpeg")
                failures.Add("gövde/içerik türü");
        }

        var bare = url[..url.IndexOf('?')];

        using (var anonymous = await http.GetAsync(bare))
            Check("imzasiz url", anonymous.StatusCode, HttpStatusCode.Forbidden);

        var tampered = url.Replace("X-Amz-Signature=", "X-Amz-Signature=0", StringComparison.Ordinal);

        using (var bad = await http.GetAsync(tampered))
            Check("bozulmus imza", bad.StatusCode, HttpStatusCode.Forbidden);

        var shortLived = storage.CreateDownloadUrl(key, TimeSpan.FromSeconds(2));
        await Task.Delay(TimeSpan.FromSeconds(4));

        using (var expired = await http.GetAsync(shortLived))
            Check("süresi dolmuş url (2 sn ömürlü, 4 sn sonra)", expired.StatusCode, HttpStatusCode.Forbidden);

        await storage.DeleteAsync(key);

        using (var gone = await http.GetAsync(storage.CreateDownloadUrl(key, TimeSpan.FromSeconds(60))))
            Check("silindikten sonra", gone.StatusCode, HttpStatusCode.NotFound);

        var report = Environment.GetEnvironmentVariable("SCOOTLY_LIVE_STORAGE_REPORT");

        if (!string.IsNullOrWhiteSpace(report))
            await File.WriteAllLinesAsync(report, observed);

        Assert.True(failures.Count == 0, "Beklenmeyen: " + string.Join(", ", failures) + " | " + string.Join(" | ", observed));
    }
}
