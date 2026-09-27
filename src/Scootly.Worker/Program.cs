using Microsoft.EntityFrameworkCore;
using Scootly.Application.Abstractions;
using Scootly.Application.Behaviors;
using Scootly.Application.Billing.Commands;
using Scootly.Application.FieldOps.Commands;
using Scootly.Application.IntegrationEvents;
using Scootly.Application.Pricing.Commands;
using Scootly.Application.Riding.Commands;
using Scootly.Infrastructure.Messaging;
using Scootly.Infrastructure.Messaging.Consumers;
using Scootly.Infrastructure.Messaging.Idempotency;
using Scootly.Infrastructure.Messaging.Outbox;
using Scootly.Infrastructure.Payments;
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
builder.Services.AddScoped<IOutstandingDebtRepository, OutstandingDebtRepository>();
builder.Services.AddScoped<IClock, SystemClock>();

builder.Services.AddScoped<CancelReservationCommandHandler>();

// --- Hafta 13: mesajlasma -----------------------------------------------------
// Tuketiciler Worker'da, API'de degil: API'yi yatay olceklersen tuketiciler
// de ikiye katlanirdi.
var rabbitMq = builder.Configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
               ?? new RabbitMqOptions();
rabbitMq.ClientName = "scootly-worker";
builder.Services.AddScootlyMessaging(rabbitMq);

// --- Hafta 14: outbox, idempotency, saga --------------------------------------
builder.Services.AddScoped<IOutboxWriter, OutboxWriter>();
builder.Services.AddScoped<OutboxDispatcher>();
builder.Services.AddScoped<IProcessedMessageStore, ProcessedMessageStore>();
builder.Services.AddScoped<IdempotencyBehavior>();

var odeme = builder.Configuration.GetSection(PaymentOptions.SectionName).Get<PaymentOptions>()
            ?? new PaymentOptions();
builder.Services.AddSingleton(odeme);
// 71. gunde gercek odeme simulatorunun istemcisiyle degisecek.
builder.Services.AddSingleton<IPaymentGateway, FakePaymentGateway>();

builder.Services.AddScoped<ApplyRideFareCommandHandler>();
builder.Services.AddScoped<OpenBatteryFieldTaskCommandHandler>();
builder.Services.AddScoped<AuthorizeRidePaymentCommandHandler>();
builder.Services.AddScoped<SettleRidePaymentCommandHandler>();

builder.Services.AddScoped<RideCompletedConsumer>();
builder.Services.AddScoped<BatteryLowConsumer>();
builder.Services.AddScoped<PaymentAuthorizationRequestedConsumer>();
builder.Services.AddScoped<PaymentAuthorizedConsumer>();

builder.Services.AddHostedService<OutboxDispatcherService>();

// Saga (koreografi): surus bitti -> ucret -> odeme -> sonuc -> arac serbest.
// Merkezi bir koordinator yok; her adim bir oncekinin olayini dinleyip
// kendi adimini atiyor ve bir sonrakinin olayini outbox'a yaziyor.
builder.Services.AddHostedService<RabbitMqConsumerService<RideCompletedIntegrationEvent, RideCompletedConsumer>>();
builder.Services.AddHostedService<RabbitMqConsumerService<PaymentAuthorizationRequestedIntegrationEvent, PaymentAuthorizationRequestedConsumer>>();
builder.Services.AddHostedService<RabbitMqConsumerService<PaymentAuthorizedIntegrationEvent, PaymentAuthorizedConsumer>>();
builder.Services.AddHostedService<RabbitMqConsumerService<VehicleBatteryLowIntegrationEvent, BatteryLowConsumer>>();

builder.Services.AddHostedService<ReservationTimeoutService>();
builder.Services.AddHostedService<BatteryThresholdScanner>();
builder.Services.AddHostedService<AbandonedRideDetector>();

var host = builder.Build();
host.Run();
