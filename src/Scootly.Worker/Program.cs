using Scootly.Application;
using Scootly.Infrastructure;
using Scootly.Worker;
using Scootly.Worker.Jobs;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddScootlyApplication();
builder.Services.AddScootlyInfrastructure(builder.Configuration);
builder.Services.AddScootlyPaymentGateway();

builder.Services.AddOptions<WorkerOptions>()
    .BindConfiguration(WorkerOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHostedService<ReservationTimeoutService>();
builder.Services.AddHostedService<AbandonedRideDetector>();
builder.Services.AddHostedService<BatteryThresholdScanner>();
builder.Services.AddHostedService<PendingPaymentRetryService>();
builder.Services.AddHostedService<DataRetentionService>();

if (InfrastructureDependencyInjection.IsMessagingEnabled(builder.Configuration))
    builder.Services.AddHostedService<BatteryLowConsumer>();

var host = builder.Build();
host.Run();
