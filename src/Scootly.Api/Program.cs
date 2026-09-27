using Asp.Versioning;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Scootly.Api.ErrorHandling;
using Scootly.Api.Extensions;
using Scootly.Api.Hubs;
using Scootly.Api.Logging;
using Scootly.Api.Services;
using Scootly.Api.Validators;
using Scootly.Application;
using Scootly.Application.Abstractions;
using Scootly.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Destructure.With<SensitiveDataDestructuringPolicy>()
        .WriteTo.Console();
});

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
})
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

    options.AddPolicy("ScootlyWebPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Ters vekil sunucu arkasında gerçek istemci IP'si (rate limiting bölümlemesi için) yalnızca tanımlı vekillerden kabul edilir.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
        options.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
});

builder.Services.AddScootlyRateLimiting();
builder.Services.AddScootlySwagger();

builder.Services.AddScootlyApplication();
builder.Services.AddScootlyInfrastructure(builder.Configuration);
builder.Services.AddScootlyIdentity();
builder.Services.AddScootlyPaymentGateway();
builder.Services.AddScootlyPaymentWebhooks();
builder.Services.AddScootlyAuthentication();
builder.Services.AddScootlyAuthorization();

builder.Services.AddScoped<IFleetNotifier, SignalRFleetNotifier>();

builder.Services.AddSingleton<CredentialsValidator>();
builder.Services.AddSingleton<StartRideRequestValidator>();
builder.Services.AddSingleton<CompleteRideRequestValidator>();
builder.Services.AddSingleton<RegisterVehicleRequestValidator>();
builder.Services.AddSingleton<VehicleQueryValidator>();
builder.Services.AddSingleton<TelemetryBatchRequestValidator>();
builder.Services.AddSingleton<CreateServiceAreaRequestValidator>();

builder.Services.AddHostedService<TelemetryChannelConsumer>();

if (InfrastructureDependencyInjection.IsMessagingEnabled(builder.Configuration))
{
    builder.Services.AddHostedService<RideChargeConsumer>();
    builder.Services.AddHostedService<VehicleStatusNotificationConsumer>();
}

builder.Services.AddHealthChecks().AddScootlyHealthChecks(builder.Configuration);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseScootlySwagger();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseSerilogRequestLogging();

app.UseCors("ScootlyWebPolicy");

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHub<FleetHub>($"{SecurityExtensions.HubsPathPrefix}/fleet");

// Canlılık: süreç ayakta mı? Hazırlık: bağımlılıklar (veritabanı, önbellek, mesaj kuyruğu) erişilebilir mi?
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

app.Run();

public partial class Program { }
