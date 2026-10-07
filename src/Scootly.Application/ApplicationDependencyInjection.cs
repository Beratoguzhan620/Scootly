using Microsoft.Extensions.DependencyInjection;
using Scootly.Application.FieldOps.Commands;
using Scootly.Application.Fleet.Commands;
using Scootly.Application.Payments.Commands;
using Scootly.Application.Riding.Commands;
using Scootly.Application.Telemetry;

namespace Scootly.Application;

public static class ApplicationDependencyInjection
{
    /// <summary>Filo, sürüş, saha ve telemetri handler'ları (Api, Worker ve Mvc ortak).</summary>
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

        services.AddScoped<ProcessTelemetryBatchCommandHandler>();
        services.AddSingleton<TelemetryChannel>();

        return services;
    }

    /// <summary>
    /// Ödeme saga'sının handler'ları. Bir <c>IPaymentGateway</c> kaydı gerektirir; yalnızca tahsilat yapan
    /// süreçler (Api, Worker) çağırır.
    /// </summary>
    public static IServiceCollection AddScootlyPayments(this IServiceCollection services)
    {
        services.AddScoped<ChargeRideCommandHandler>();
        services.AddScoped<ApplyPaymentWebhookCommandHandler>();

        return services;
    }
}
