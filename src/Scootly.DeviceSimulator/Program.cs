using Microsoft.Extensions.Configuration;
using Scootly.DeviceSimulator;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddUserSecrets<SimulatorOptions>(optional: true)
    .AddEnvironmentVariables(prefix: "SCOOTLY_")
    .AddCommandLine(args)
    .Build();

var options = configuration.Get<SimulatorOptions>() ?? new SimulatorOptions();

if (string.IsNullOrWhiteSpace(options.ClientSecret))
{
    Console.Error.WriteLine(
        "Cihaz sırrı yapılandırılmamış. Örnek: dotnet user-secrets set ClientSecret \"<sır>\" --project src/Scootly.DeviceSimulator " +
        "veya SCOOTLY_ClientSecret ortam değişkeni.");
    return 1;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

using var httpClient = new HttpClient { BaseAddress = new Uri(options.ApiBaseUrl) };
var publisher = new TelemetryPublisher(httpClient, options);

try
{
    Console.WriteLine("Cihaz kimlik doğrulaması yapılıyor...");
    await publisher.AuthenticateAsync(cancellation.Token);
    Console.WriteLine("Kimlik doğrulama başarılı.");

    var vehicles = await publisher.LoadRegisteredVehiclesAsync(cancellation.Token);

    if (vehicles.Count == 0)
    {
        Console.Error.WriteLine("API'de kayıtlı araç yok. Önce bir filo yöneticisi hesabıyla araç kaydedin (POST /api/v1/vehicles).");
        return 1;
    }

    Console.WriteLine($"{vehicles.Count} kayıtlı araç simüle ediliyor. Ctrl+C ile durdurun.");

    var interval = TimeSpan.FromSeconds(Math.Max(1, options.IntervalSeconds));
    var failureBackoff = interval;

    while (!cancellation.IsCancellationRequested)
    {
        foreach (var vehicle in vehicles)
        {
            vehicle.Move();
            vehicle.DrainBattery();
        }

        try
        {
            var published = await publisher.PublishAsync(vehicles, cancellation.Token);
            failureBackoff = published ? interval : TimeSpan.FromSeconds(Math.Min(failureBackoff.TotalSeconds * 2, 60));
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"[{DateTime.Now:HH:mm:ss}] API'ye ulaşılamadı: {ex.Message}");
            failureBackoff = TimeSpan.FromSeconds(Math.Min(failureBackoff.TotalSeconds * 2, 60));
        }

        await Task.Delay(failureBackoff, cancellation.Token);
    }
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
    Console.WriteLine("Simülatör durduruldu.");
}
catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

return 0;
