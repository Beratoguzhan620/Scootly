using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Scootly.Infrastructure.Identity;

namespace Scootly.Api.Demo;

/// <summary>
/// Herkese açık demoyu Api'nin kendi uçları üzerinden kurar ve canlı tutar; böylece demodaki her şey bir
/// ziyaretçinin yaptığıyla aynı kurallardan geçer. Açılışta bir kez: filo yöneticisi olarak Samsun hizmet bölgesini
/// tanımlar, araçları kaydeder ve demo sürücülerini açar. Sonra servis uyanık kaldıkça sürücüler müsait bir araçla
/// sürüşe başlar ve birkaç dakika sonra bölge içinde başka bir noktada bitirir; ücret, Worker'ın tahsilat işiyle
/// ödeme simülatöründen alınır. Panelde sürüşler, ödemeler ve araç konumları böylece hep güncel görünür.
/// </summary>
internal sealed partial class DemoWorker(
    IOptions<DemoOptions> options, IOptions<BootstrapOptions> bootstrap, IHttpClientFactory clients, IHostApplicationLifetime lifetime,
    TimeProvider clock, ILogger<DemoWorker> logger)
    : BackgroundService
{
    public const string HttpClientName = "Scootly.Demo";

    private const string AreaName = "Samsun Merkez";

    // Atakum'dan Canik'e sahil şeridi; saat yönünde.
    private static readonly (double Lat, double Lng)[] Boundary =
    [
        (41.3480, 36.2350), (41.3200, 36.3250), (41.2950, 36.3550), (41.2680, 36.3800),
        (41.2550, 36.3550), (41.2780, 36.3150), (41.3050, 36.2650), (41.3300, 36.2300),
    ];

    // Bölgenin içinde, insanların gittiği yerler: sürüşler bunlardan birinde biter.
    private static readonly (double Lat, double Lng)[] Places =
    [
        (41.3332, 36.2647), (41.3210, 36.2940), (41.3080, 36.3060), (41.2947, 36.3330), (41.2867, 36.3300),
        (41.2905, 36.3205), (41.2780, 36.3420), (41.2720, 36.3550), (41.3150, 36.2850), (41.2990, 36.3150),
    ];

    private static readonly string[] Brands = ["Xiaomi Pro 2", "Segway Ninebot Max", "NIU KQi3"];

    private static readonly string[] Riders = ["ayse", "mert", "zeynep"];

    private readonly Random _random = new(26);
    private readonly Dictionary<string, string> _tokens = [];
    private string _manager = "";
    private DateTimeOffset _signedInAt;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!await StartedAsync(stoppingToken))
        {
            return;
        }

        using var http = clients.CreateClient(HttpClientName);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_tokens.Count == 0 || clock.GetUtcNow() - _signedInAt > TimeSpan.FromMinutes(45))
                {
                    await SetUpAsync(http, stoppingToken);
                }

                await TickAsync(http, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Demo devam eder; bir sonraki tur yeniden dener.
                LogTickFailed(logger, ex);
            }

            await Task.Delay(options.Value.Tick, clock, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> StartedAsync(CancellationToken ct)
    {
        var started = new TaskCompletionSource();
        using var registration = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        await Task.WhenAny(started.Task, Task.Delay(Timeout.Infinite, ct)).ConfigureAwait(false);
        return started.Task.IsCompleted && !ct.IsCancellationRequested;
    }

    /// <summary>Herkesi oturum açtırır; ilk seferde bölgeyi, araçları ve sürücüleri oluşturur.</summary>
    public async Task SetUpAsync(HttpClient http, CancellationToken ct)
    {
        var manager = bootstrap.Value;
        if (string.IsNullOrWhiteSpace(manager.FleetManagerEmail) || string.IsNullOrWhiteSpace(manager.FleetManagerPassword))
        {
            throw new InvalidOperationException("Demo, Bootstrap:FleetManagerEmail ve Bootstrap:FleetManagerPassword ister.");
        }

        _manager = await LoginAsync(http, manager.FleetManagerEmail, manager.FleetManagerPassword, ct)
            ?? throw new InvalidOperationException("Demo filo yöneticisi oturum açamadı.");

        var areas = await SendAsync<List<JsonElement>>(http, HttpMethod.Get, "/api/v1/service-areas", _manager, null, ct);
        if (!areas.Any(a => a.GetProperty("name").GetString() == AreaName))
        {
            await SendAsync<JsonElement?>(http, HttpMethod.Post, "/api/v1/service-areas", _manager, new
            {
                name = AreaName,
                boundary = Boundary.Select(p => new { latitude = p.Lat, longitude = p.Lng }),
            }, ct);
        }

        var fleet = await SendAsync<VehiclePage>(http, HttpMethod.Get, "/api/v2/vehicles?pageSize=50", _manager, null, ct);
        for (var i = fleet.TotalCount; i < 16; i++)
        {
            var (lat, lng) = Places[i % Places.Length];
            await SendAsync<JsonElement?>(http, HttpMethod.Post, "/api/v1/vehicles", _manager, new
            {
                brand = Brands[i % Brands.Length],
                rangeKm = 40 + (i % 3 * 10),
                latitude = lat + ((_random.NextDouble() - 0.5) * 0.004),
                longitude = lng + ((_random.NextDouble() - 0.5) * 0.004),
                batteryPercentage = 35 + _random.Next(65),
            }, ct);
        }

        foreach (var rider in Riders)
        {
            var email = $"{rider}@riders.scootly.demo";
            var token = await LoginAsync(http, email, options.Value.RiderPassword, ct);
            if (token is null)
            {
                using var register = await http.PostAsJsonAsync("/api/auth/register", new { email, password = options.Value.RiderPassword }, ct);
                token = await LoginAsync(http, email, options.Value.RiderPassword, ct)
                    ?? throw new InvalidOperationException($"Demo sürücüsü {email} açılamadı: {(int)register.StatusCode}.");
            }

            _tokens[rider] = token;
        }

        _signedInAt = clock.GetUtcNow();
        LogReady(logger, AreaName);
    }

    /// <summary>Her sürücü için bir adım: süren bitirir, sürmeyen bazen yeni bir sürüşe başlar.</summary>
    public async Task TickAsync(HttpClient http, CancellationToken ct)
    {
        foreach (var (rider, token) in _tokens)
        {
            var active = await SendAsync<Ride?>(http, HttpMethod.Get, "/api/rides/active", token, null, ct, allowNotFound: true);
            if (active is not null)
            {
                if (clock.GetUtcNow() - new DateTimeOffset(DateTime.SpecifyKind(active.StartedAt, DateTimeKind.Utc)) >= options.Value.RideLength)
                {
                    var (lat, lng) = Places[_random.Next(Places.Length)];
                    await SendAsync<JsonElement?>(http, HttpMethod.Post, $"/api/rides/{active.Id}/complete", token,
                        new { endLatitude = lat, endLongitude = lng }, ct);
                }

                continue;
            }

            // Sürücüler aynı anda değil, ara ara yola çıkar.
            if (_random.Next(3) != 0)
            {
                continue;
            }

            var available = await SendAsync<VehiclePage>(http, HttpMethod.Get, "/api/v2/vehicles?onlyAvailable=true&pageSize=50", token, null, ct);
            var candidates = available.Items.Where(v => v.BatteryPercentage >= 25).ToList();
            if (candidates.Count == 0)
            {
                continue;
            }

            // Gerçek bir sürücü gibi önce rezerve eder; araç bu arada başkasına geçtiyse bu tur bekler.
            var vehicle = candidates[_random.Next(candidates.Count)];
            using (var reserve = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/vehicles/{vehicle.Id}/reserve"))
            {
                reserve.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var reserved = await http.SendAsync(reserve, ct);
                if (!reserved.IsSuccessStatusCode)
                {
                    continue;
                }
            }

            await SendAsync<JsonElement?>(http, HttpMethod.Post, "/api/rides/start", token, new { vehicleId = vehicle.Id }, ct);
            LogRideStarted(logger, rider, vehicle.Id);
        }
    }

    private static async Task<string?> LoginAsync(HttpClient http, string email, string password, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("/api/auth/login", new { email, password }, ct);
        return response.IsSuccessStatusCode ? (await response.Content.ReadFromJsonAsync<TokenBody>(ct))!.Token : null;
    }

    private static async Task<T> SendAsync<T>(
        HttpClient http, HttpMethod method, string path, string token, object? body, CancellationToken ct, bool allowNotFound = false)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await http.SendAsync(request, ct);
        if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
        {
            return default!;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"{method} {path} {(int)response.StatusCode} döndü: {await response.Content.ReadAsStringAsync(ct)}");
        }

        return response.StatusCode == HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0
            ? default!
            : (await response.Content.ReadFromJsonAsync<T>(ct))!;
    }

    private sealed record TokenBody(string Token);

    private sealed record Ride(Guid Id, DateTime StartedAt);

    private sealed record Vehicle(Guid Id, int BatteryPercentage);

    private sealed record VehiclePage(List<Vehicle> Items, int TotalCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo hizmet bölgesi {Area} hazır")]
    private static partial void LogReady(ILogger log, string area);

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo sürücüsü {Rider}, {VehicleId} aracıyla yola çıktı")]
    private static partial void LogRideStarted(ILogger log, string rider, Guid vehicleId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bir demo turu başarısız oldu")]
    private static partial void LogTickFailed(ILogger log, Exception exception);
}
