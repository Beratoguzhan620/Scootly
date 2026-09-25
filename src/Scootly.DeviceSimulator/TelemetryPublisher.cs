using System.Net.Http.Json;

namespace Scootly.DeviceSimulator;

public sealed class TelemetryPublisher
{
    private readonly HttpClient _httpClient;
    private string? _token;

    public TelemetryPublisher(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task AuthenticateAsync(string clientId, string clientSecret)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/device-auth/token", new
        {
            ClientId = clientId,
            ClientSecret = clientSecret
        });

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TokenResponse>();
        _token = result!.Token;

        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
    }

    public async Task PublishAsync(IReadOnlyList<SimulatedVehicle> vehicles)
    {
        var readings = vehicles.Select(v => new
        {
            VehicleId = v.VehicleId,
            Latitude = v.Latitude,
            Longitude = v.Longitude,
            BatteryPercentage = v.BatteryPercentage
        });

        var response = await _httpClient.PostAsJsonAsync("/api/telemetry/batch", new { Readings = readings });

        if (response.IsSuccessStatusCode)
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {vehicles.Count} araç için telemetri gönderildi.");
        else
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Telemetri gönderimi başarısız: {response.StatusCode}");
    }

    private sealed record TokenResponse(string Token);
}