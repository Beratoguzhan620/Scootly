using Scootly.Infrastructure;
using Scootly.Worker.Jobs;

// Scootly arka plan işçisi (54. gün).
//
// API'den AYRI BİR SÜREÇ olmasının gerekçesi: bu üç servis API'nin içinde de
// çalışabilirdi, ama o zaman API'yi yatay ölçeklediğin anda (20. hafta, Nginx
// arkasında iki kopya) her kopya kendi zamanlayıcısını çalıştırır ve aynı
// rezervasyon iki kez düşürülmeye çalışılır. Ayrı süreç, "bu iş tek bir yerde
// çalışsın" demenin en basit yolu.
//
// Bu henüz tam bir çözüm değil: Worker'ın kendisi iki kopya çalıştırılırsa
// aynı problem geri gelir. Gerçek çözüm bir lider seçimi (leader election) ya
// da 50. günde yazılan dağıtık kilit — teknik borç listesinde.

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddScootlyInfrastructure(builder.Configuration);

builder.Services.AddHostedService<ReservationTimeoutService>();
builder.Services.AddHostedService<BatteryThresholdScanner>();
builder.Services.AddHostedService<AbandonedRideDetector>();

var host = builder.Build();

await host.RunAsync();
