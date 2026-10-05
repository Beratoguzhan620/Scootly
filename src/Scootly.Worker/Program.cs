using Scootly.Application;
using Scootly.Infrastructure;
using Scootly.Infrastructure.Logging;
using Scootly.Infrastructure.Observability;
using Scootly.Worker;
using Scootly.Worker.Jobs;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

var loggerConfiguration = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "Scootly.Worker")
    .Destructure.With<SensitiveDataDestructuringPolicy>()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");

var seqUrl = builder.Configuration["Seq:ServerUrl"];

if (!string.IsNullOrWhiteSpace(seqUrl))
    loggerConfiguration.WriteTo.Seq(seqUrl);

Log.Logger = loggerConfiguration.CreateLogger();
builder.Services.AddSerilog();

builder.Services.AddScootlyApplication();
builder.Services.AddScootlyInfrastructure(builder.Configuration);
builder.Services.AddScootlyTelemetry(builder.Configuration, "Scootly.Worker");
builder.Services.AddScootlyPaymentGateway();

builder.Services.AddOptions<WorkerOptions>()
    .BindConfiguration(WorkerOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHostedService<WorkerHeartbeatService>();
builder.Services.AddHostedService<ReservationTimeoutService>();
builder.Services.AddHostedService<AbandonedRideDetector>();
builder.Services.AddHostedService<BatteryThresholdScanner>();
builder.Services.AddHostedService<PendingPaymentRetryService>();
builder.Services.AddHostedService<DataRetentionService>();

if (InfrastructureDependencyInjection.IsMessagingEnabled(builder.Configuration))
    builder.Services.AddHostedService<BatteryLowConsumer>();

var host = builder.Build();
host.Run();