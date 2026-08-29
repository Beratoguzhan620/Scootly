using Microsoft.Extensions.DependencyInjection;
using Scootly.Application.Abstractions;
using Scootly.Infrastructure.Persistence;
using Scootly.Infrastructure.Time;

namespace Scootly.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();

        // GEÇİCİ: bellekteki veri istekler arasında yaşasın diye singleton
        services.AddSingleton<InMemoryApplicationDbContext>();
        services.AddSingleton<IApplicationDbContext>(sp => sp.GetRequiredService<InMemoryApplicationDbContext>());
        services.AddSingleton<IUnitOfWork>(sp => sp.GetRequiredService<InMemoryApplicationDbContext>());

        return services;
    }
}