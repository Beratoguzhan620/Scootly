using System.Globalization;
using Scootly.DeviceSimulator;

// Scootly cihaz simülatörü (53. gün).
//
// Gerçek scooter donanımı olmayan bir projede gerçekçi yük üretmenin yolu.
// 11. haftadan itibaren telemetri hattını, 15. haftada yük testini besleyecek.
//
// Çalıştırma:
//   SCOOTLY_API=http://localhost:5000 \
//   SCOOTLY_DEVICE_SECRET=<sir> \
//   dotnet run --project src/Scootly.DeviceSimulator
//
// Cihaz sırrı ORTAM DEĞİŞKENİNDEN okunuyor, kodda varsayılanı yok: kaynağa
// yazılmış bir varsayılan sır, o sırrın üretimde de kullanılmasıyla biter.

var apiTabani = Environment.GetEnvironmentVariable("SCOOTLY_API") ?? "http://localhost:5000";
var sir = Environment.GetEnvironmentVariable("SCOOTLY_DEVICE_SECRET");
var onek = Environment.GetEnvironmentVariable("SCOOTLY_DEVICE_PREFIX") ?? "sim-";

if (string.IsNullOrWhiteSpace(sir))
{
    Console.Error.WriteLine("SCOOTLY_DEVICE_SECRET tanimli degil.");
    Console.Error.WriteLine("API'yi Seed:SimulatorDevices ayariyla baslatip ayni sirri buraya verin.");
    return 1;
}

var aracSayisi = OkuSayi("SCOOTLY_DEVICE_COUNT", 200);
var esZamanlilik = OkuSayi("SCOOTLY_CONCURRENCY", 16);

// Adana merkez civarı — araçlar buradan başlayıp rastgele yürüyor.
const double MerkezEnlem = 37.0000d;
const double MerkezBoylam = 35.3213d;

const int TurAraligiSaniye = 1;
const int GonderimPeriyodu = 5;

using var http = new HttpClient
{
    BaseAddress = new Uri(apiTabani, UriKind.Absolute),
    Timeout = TimeSpan.FromSeconds(30)
};

var araclar = new List<SimulatedVehicle>(aracSayisi);
var yayincilar = new Dictionary<string, TelemetryPublisher>(aracSayisi, StringComparer.Ordinal);
var birikmis = new Dictionary<string, List<TelemetryReadingRequest>>(aracSayisi, StringComparer.Ordinal);

for (var i = 1; i <= aracSayisi; i++)
{
    var cihazId = onek + i.ToString("D4", CultureInfo.InvariantCulture);

    araclar.Add(new SimulatedVehicle(
        cihazId,
        MerkezEnlem + ((Random.Shared.NextDouble() - 0.5) * 0.05),
        MerkezBoylam + ((Random.Shared.NextDouble() - 0.5) * 0.05)));

    yayincilar[cihazId] = new TelemetryPublisher(http, cihazId, sir);
    birikmis[cihazId] = [];
}

using var iptal = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    // Ctrl+C'de süreci öldürmek yerine düzgün kapanış: açık HTTP istekleri
    // tamamlansın ve özet yazılsın.
    e.Cancel = true;
    iptal.Cancel();
};

Console.WriteLine($"{aracSayisi} sanal arac basladi. API: {apiTabani}");
Console.WriteLine("Durdurmak icin Ctrl+C.");

var tur = 0;

try
{
    using var zamanlayici = new PeriodicTimer(TimeSpan.FromSeconds(TurAraligiSaniye));

    while (await zamanlayici.WaitForNextTickAsync(iptal.Token))
    {
        tur++;

        var simdi = DateTime.UtcNow;

        foreach (var arac in araclar)
        {
            arac.Move();
            arac.DrainBattery();

            birikmis[arac.DeviceId].Add(new TelemetryReadingRequest(
                arac.DeviceId, arac.Latitude, arac.Longitude, arac.BatteryPercentage, simdi));
        }

        if (tur % GonderimPeriyodu != 0)
        {
            continue;
        }

        // Her cihaz kendi yığınını gönderiyor. Eş zamanlılık sınırlı: 200
        // isteği aynı anda açmak, ölçmek istediğimiz sunucu davranışı yerine
        // istemci tarafındaki bağlantı kuyruğunu ölçmekle sonuçlanırdı.
        await Parallel.ForEachAsync(
            araclar,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = esZamanlilik,
                CancellationToken = iptal.Token
            },
            async (arac, ct) =>
            {
                var yigin = birikmis[arac.DeviceId];

                if (yigin.Count == 0)
                {
                    return;
                }

                // Kopya gönderiliyor: liste hemen temizleniyor ki bir sonraki
                // tur yazarken gönderim devam ediyor olsa bile çakışma olmasın.
                var gonderilecek = yigin.ToArray();
                yigin.Clear();

                await yayincilar[arac.DeviceId].PublishAsync(gonderilecek, ct);
            });
    }
}
catch (OperationCanceledException)
{
    // Ctrl+C. Normal kapanış.
}

var toplamOlcum = yayincilar.Values.Sum(y => y.GonderilenOlcum);
var toplamHata = yayincilar.Values.Sum(y => y.BasarisizIstek);

Console.WriteLine();
Console.WriteLine($"Durduruldu. Gonderilen olcum: {toplamOlcum}, basarisiz istek: {toplamHata}");

return 0;

static int OkuSayi(string degiskenAdi, int varsayilan)
{
    var ham = Environment.GetEnvironmentVariable(degiskenAdi);

    return int.TryParse(ham, NumberStyles.Integer, CultureInfo.InvariantCulture, out var deger) && deger > 0
        ? deger
        : varsayilan;
}
