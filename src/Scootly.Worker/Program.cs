using Microsoft.EntityFrameworkCore;
using Scootly.Application.Abstractions;
using Scootly.Application.FieldOps.Commands;
using Scootly.Application.IntegrationEvents;
using Scootly.Application.Pricing.Commands;
using Scootly.Application.Riding.Commands;
using Scootly.Infrastructure.Messaging;
using Scootly.Infrastructure.Messaging.Consumers;
using Scootly.Infrastructure.Persistence;
using Scootly.Infrastructure.Persistence.Repositories;
using Scootly.Infrastructure.Time;
using Scootly.Worker.Jobs;
using Scootly.Worker.Messaging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<ScootlyDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IUnitOfWork>(provider =>
    provider.GetRequiredService<ScootlyDbContext>());

builder.Services.AddScoped<IVehicleRepository, VehicleRepository>();
builder.Services.AddScoped<IRideRepository, RideRepository>();
builder.Services.AddScoped<IFieldTaskRepository, FieldTaskRepository>();
builder.Services.AddScoped<IClock, SystemClock>();

builder.Services.AddScoped<CancelReservationCommandHandler>();

// --- 61-65. gunler: mesajlasma ------------------------------------------------
// Tuketiciler Worker'da, API'de degil. API'yi yatay olceklersen (20. hafta,
// Nginx arkasinda iki kopya) tuketiciler de ikiye katlanirdi; ucret hesabi gibi
// isleri istek karsilayan surecten ayirmanin amaci zaten bu.
var rabbitMq = builder.Configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
               ?? new RabbitMqOptions();
rabbitMq.ClientName = "scootly-worker";
builder.Services.AddScootlyMessaging(rabbitMq);

builder.Services.AddScoped<ApplyRideFareCommandHandler>();
builder.Services.AddScoped<OpenBatteryFieldTaskCommandHandler>();

builder.Services.AddScoped<RideCompletedConsumer>();
builder.Services.AddScoped<BatteryLowConsumer>();

builder.Services.AddHostedService<RabbitMqConsumerService<RideCompletedIntegrationEvent, RideCompletedConsumer>>();
builder.Services.AddHostedService<RabbitMqConsumerService<VehicleBatteryLowIntegrationEvent, BatteryLowConsumer>>();

builder.Services.AddHostedService<ReservationTimeoutService>();
builder.Services.AddHostedService<BatteryThresholdScanner>();
builder.Services.AddHostedService<AbandonedRideDetector>();

var host = builder.Build();
host.Run();
