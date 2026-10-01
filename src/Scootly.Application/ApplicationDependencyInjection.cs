using Microsoft.Extensions.DependencyInjection;
using Scootly.Application.FieldOps.Commands;
using Scootly.Application.Fleet.Commands;
using Scootly.Application.Payments.Commands;
using Scootly.Application.Riding.Commands;
using Scootly.Application.Telemetry;

namespace Scootly.Application;

public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddScootlyApplication(this IServiceCollection services)
    {
        services.AddScoped<RegisterVehicleCommandHandler>();
        services.AddScoped<VehicleMaintenanceCommandHandler>();
        services.AddScoped<UpdateVehicleDetailsCommandHandler>();
        services.AddScoped<CreateServiceAreaCommandHandler>();
        services.AddScoped<FieldTaskCommandHandler>();

        services.AddScoped<ReserveVehicleCommandHandler>();
        services.AddScoped<CancelReservationCommandHandler>();
        services.AddScoped<ExpireReservationCommandHandler>();
        services.AddScoped<StartRideCommandHandler>();
        services.AddScoped<CompleteRideCommandHandler>();
        services.AddScoped<AbandonRideCommandHandler>();

        services.AddScoped<ChargeRideCommandHandler>();
        services.AddScoped<ApplyPaymentWebhookCommandHandler>();

        services.AddScoped<ProcessTelemetryBatchCommandHandler>();
        services.AddSingleton<TelemetryChannel>();

        return services;
    }
}