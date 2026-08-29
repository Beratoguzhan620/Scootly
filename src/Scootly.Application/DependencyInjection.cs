using Microsoft.Extensions.DependencyInjection;
using Scootly.Application.Riding.Commands;

namespace Scootly.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ReserveVehicleCommandHandler>();
        services.AddScoped<StartRideCommandHandler>();
        services.AddScoped<CompleteRideCommandHandler>();

        return services;
    }
}