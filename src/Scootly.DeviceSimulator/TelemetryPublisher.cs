using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Scootly.DeviceSimulator;

public sealed class TelemetryPublisher
{
    private readonly HttpClient _httpClient;
    private readonly SimulatorOptions _options;

    public TelemetryPublisher(HttpClient httpClient, SimulatorOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/device-auth/token", new
        {
            _options.ClientId,
            _options.ClientSecret
        }, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("Cihaz kimlik bilgileri reddedildi. ClientId/ClientSecret yapılandırmasını kontrol edin.");

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result!.Token);
    }

    /// <summary>API'de kayıtlı araçları (herkese açık liste ucundan) sayfa sayfa okur.</summary>
    public async Task<IReadOnlyList<SimulatedVehicle>> LoadRegisteredVehiclesAsync(CancellationToken cancellationToken)
    {
        const int pageSize = 100;
        var vehicles = new List<SimulatedVehicle>();

        for (var page = 1; vehicles.Count < _options.MaxVehicles; page++)
        {
            var result = await _httpClient.GetFromJsonAsync<VehiclePage>(
                $"/api/v2/vehicles?pageNumber={page}&pageSize={pageSize}", cancellationToken);

            if (result is null || result.Items.Count == 0)
                break;

            vehicles.AddRange(result.Items.Select(v => new SimulatedVehicle(v.Id, v.Latitude, v.Longitude, v.BatteryPercentage)));

            if (page >= result.TotalPages)
                break;
        }

        return vehicles.Take(_options.MaxVehicles).ToList();
    }

    /// <returns>Gönderim başarılıysa true; token süresi dolduysa bir kez yeniden kimlik doğrular.</returns>
    public async Task<bool> PublishAsync(IReadOnlyList<SimulatedVehicle> vehicles, CancellationToken cancellationToken)
    {
        var response = await SendAsync(vehicles, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await AuthenticateAsync(cancellationToken);
            response = await SendAsync(vehicles, cancellationToken);
        }

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {vehicles.Count} araç için telemetri gönderildi.");
            return true;
        }

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Telemetri gönderimi başarısız: {(int)response.StatusCode} {response.StatusCode}");
        return false;
    }

    private Task<HttpResponseMessage> SendAsync(IReadOnlyList<SimulatedVehicle> vehicles, CancellationToken cancellationToken)
    {
        var recordedAt = DateTime.UtcNow;

        var readings = vehicles.Select(v => new
        {
            v.VehicleId,
            v.Latitude,
            v.Longitude,
            v.BatteryPercentage,
            RecordedAt = recordedAt
        });

        return _httpClient.PostAsJsonAsync("/api/telemetry/batch", new { Readings = readings }, cancellationToken);
    }

    private sealed record TokenResponse(string Token);

    private sealed record VehiclePage(IReadOnlyList<VehicleItem> Items, int TotalPages);

    private sealed record VehicleItem(Guid Id, double Latitude, double Longitude, int BatteryPercentage);
}
