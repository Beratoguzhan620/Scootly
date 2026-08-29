using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scootly.Application.Abstractions;
using Scootly.Infrastructure.Persistence;
using Scootly.Infrastructure.Time;

namespace Scootly.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IClock, SystemClock>();

        services.AddDbContext<ScootlyDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Scootly")));

        // GEÇİCİ: veri hâlâ bellekten okunuyor, Gün 14'te ScootlyDbContext devralacak
        services.AddSingleton<InMemoryApplicationDbContext>();
        services.AddSingleton<IApplicationDbContext>(sp => sp.GetRequiredService<InMemoryApplicationDbContext>());
        services.AddSingleton<IUnitOfWork>(sp => sp.GetRequiredService<InMemoryApplicationDbContext>());

        return services;
    }
}