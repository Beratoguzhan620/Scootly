using Scootly.DeviceSimulator;

var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5016") };
var publisher = new TelemetryPublisher(httpClient);

Console.WriteLine("Cihaz kimlik doğrulaması yapılıyor...");
await publisher.AuthenticateAsync("scootly-device-simulator", "cihaz-gizli-anahtari-degistir-2026");
Console.WriteLine("Kimlik doğrulama başarılı.");

var random = new Random();
var vehicles = new List<SimulatedVehicle>();

for (var i = 0; i < 200; i++)
{
    vehicles.Add(new SimulatedVehicle(
        Guid.NewGuid(),
        41.0 + random.NextDouble() * 0.1,
        29.0 + random.NextDouble() * 0.1,
        random.Next(20, 100)));
}

Console.WriteLine($"{vehicles.Count} sanal araç oluşturuldu. Ctrl+C ile durdurun.");

while (true)
{
    foreach (var vehicle in vehicles)
    {
        vehicle.Move();
        vehicle.DrainBattery();
    }

    await publisher.PublishAsync(vehicles);

    await Task.Delay(TimeSpan.FromSeconds(5));
}