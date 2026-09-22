using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Scootly.DeviceSimulator;

/// <summary>API'nin sözleşmesi — bilerek elle yazıldı (bkz. csproj notu).</summary>
internal sealed record TelemetryReadingRequest(
    string DeviceId,
    double Latitude,
    double Longitude,
    int BatteryPercentage,
    DateTime RecordedAt);

internal sealed record TelemetryBatchRequest(IReadOnlyList<TelemetryReadingRequest> Readings);

internal sealed record DeviceTokenRequest(string DeviceId, string Secret);

internal sealed record TokenResponse(string AccessToken, DateTime ExpiresAtUtc);

/// <summary>
/// Sahte araçların durumunu API'ye toplu gönderir (53. gün).
/// </summary>
/// <remarks>
/// <para>
/// <b>Token yenilemesi var.</b> Cihaz token'ı 15 dakika yaşıyor
/// (<c>DeviceTokenService</c>); simülatör saatlerce çalışacağına göre token
/// süresi dolduğunda yenilemek zorunda. Bu olmasaydı simülatör 15 dakika sonra
/// sessizce 401 almaya başlar ve "telemetri gelmiyor" diye saatlerce yanlış
/// yerde hata aranırdı.
/// </para>
/// <para>
/// <b>429 (çok fazla istek) hata sayılmıyor.</b> 55. günün oran sınırlaması
/// tam da bunun için var; simülatör limiti aştığında doğru davranış
/// <c>Retry-After</c> kadar beklemek. Yeniden denemeyi hemen yapmak, sınırın
/// koruduğu sistemi daha da zorlamak olurdu.
/// </para>
/// </remarks>
internal sealed class TelemetryPublisher
{
    private readonly HttpClient _http;
    private readonly string _deviceId;
    private readonly string _secret;

    private string? _token;
    private DateTime _tokenBitis = DateTime.MinValue;

    /// <remarks>
    /// <see cref="HttpClient"/> DIŞARIDAN veriliyor ve paylaşılıyor. 200 cihaz
    /// için 200 istemci oluşturmak, her birinin kendi bağlantı havuzunu açması
    /// ve soket tükenmesi (socket exhaustion) demek olurdu — HttpClient'ın en
    /// bilinen yanlış kullanımı.
    /// </remarks>
    public TelemetryPublisher(HttpClient http, string deviceId, string secret)
    {
        _http = http;
        _deviceId = deviceId;
        _secret = secret;
    }

    public long GonderilenOlcum { get; private set; }
    public long BasarisizIstek { get; private set; }

    /// <summary>
    /// Bu cihazın biriktirdiği ölçümleri tek istekte gönderir.
    /// </summary>
    /// <remarks>
    /// Yığındaki her ölçüm AYNI cihaza ait. Farklı cihazların ölçümlerini tek
    /// istekte göndermek mümkün değil: <c>TelemetryController</c> gövdedeki
    /// her cihaz kimliğinin token'daki kimliğe eşit olmasını şart koşuyor.
    /// Aksi halde geçerli bir cihaz token'ı olan herkes başka cihazlar adına
    /// ölçüm uydurabilirdi.
    /// </remarks>
    public async Task<bool> PublishAsync(
        IReadOnlyList<TelemetryReadingRequest> readings,
        CancellationToken cancellationToken)
    {
        var token = await TokenAlAsync(cancellationToken);

        if (token is null)
        {
            BasarisizIstek++;
            return false;
        }

        var govde = new TelemetryBatchRequest(readings);

        using var istek = new HttpRequestMessage(HttpMethod.Post, "api/v1/devices/telemetry")
        {
            Content = JsonContent.Create(govde)
        };

        istek.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var yanit = await _http.SendAsync(istek, cancellationToken);

            if (yanit.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var bekleme = yanit.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5);
                Console.WriteLine($"[{_deviceId}] Oran siniri: {bekleme.TotalSeconds:0} sn beklenecek.");
                await Task.Delay(bekleme, cancellationToken);
                return false;
            }

            if (yanit.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Token erken geçersiz kılınmış olabilir. Bir sonraki turda
                // yenisi alınsın.
                _token = null;
                BasarisizIstek++;
                return false;
            }

            if (!yanit.IsSuccessStatusCode)
            {
                var metin = await yanit.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"[{_deviceId}] {(int)yanit.StatusCode}: {Kisalt(metin)}");
                BasarisizIstek++;
                return false;
            }

            GonderilenOlcum += readings.Count;
            return true;
        }
        catch (HttpRequestException ex)
        {
            // API kapalı olabilir. Simülatörün ÇÖKMEMESİ gerekiyor: 15. haftada
            // dış servis hatalarını kontrollü üretmek için bu araç kullanılacak.
            Console.WriteLine($"[{_deviceId}] Baglanti hatasi: {ex.Message}");
            BasarisizIstek++;
            return false;
        }
    }

    private async Task<string?> TokenAlAsync(CancellationToken cancellationToken)
    {
        // Bir dakikalık güvenlik payı: tam bitiş anında istek göndermek,
        // sunucu ile saat farkı kadar bir pencerede 401 üretir.
        if (_token is not null && DateTime.UtcNow < _tokenBitis.AddMinutes(-1))
        {
            return _token;
        }

        try
        {
            using var yanit = await _http.PostAsJsonAsync(
                "api/v1/device-auth/token",
                new DeviceTokenRequest(_deviceId, _secret),
                cancellationToken);

            if (!yanit.IsSuccessStatusCode)
            {
                Console.WriteLine($"[{_deviceId}] Token alinamadi: {(int)yanit.StatusCode}");
                return null;
            }

            var sonuc = await yanit.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);

            if (sonuc is null)
            {
                return null;
            }

            _token = sonuc.AccessToken;
            _tokenBitis = sonuc.ExpiresAtUtc;

            return _token;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            Console.WriteLine($"[{_deviceId}] Token hatasi: {ex.Message}");
            return null;
        }
    }

    private static string Kisalt(string metin) =>
        metin.Length <= 200 ? metin : string.Concat(metin.AsSpan(0, 200), "...");
}
