using Microsoft.EntityFrameworkCore;
using Scootly.Application.Abstractions;
using Scootly.Application.Riding.Commands;
using Scootly.Infrastructure.Messaging;
using Scootly.Infrastructure.Persistence;
using Scootly.Infrastructure.Persistence.Repositories;
using Scootly.Infrastructure.Time;
using Scootly.Worker.Jobs;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<ScootlyDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IUnitOfWork>(provider =>
    provider.GetRequiredService<ScootlyDbContext>());

builder.Services.AddScoped<IVehicleRepository, VehicleRepository>();
builder.Services.AddScoped<IClock, SystemClock>();

builder.Services.AddScoped<CancelReservationCommandHandler>();

builder.Services.AddSingleton(new RabbitMqConnectionProvider("localhost", "scootly", "rabbit123"));

builder.Services.AddHostedService<ReservationTimeoutService>();
builder.Services.AddHostedService<BatteryThresholdScanner>();
builder.Services.AddHostedService<AbandonedRideDetector>();
builder.Services.AddHostedService<BatteryLowConsumer>();

var host = builder.Build();
host.Run();