using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Scootly.Application.Abstractions;

namespace Scootly.Infrastructure.Storage;

public static class StorageDependencyInjection
{
    public static IServiceCollection AddScootlyFileStorage(this IServiceCollection services)
    {
        services.AddOptions<StorageOptions>()
            .BindConfiguration(StorageOptions.SectionName)
            .Validate(
                StorageOptions.IsValid,
                "Storage yapılandırması geçersiz: Enabled=true iken Endpoint ve PublicEndpoint (mutlak http/https adresleri), " +
                "Bucket (S3 adlandırma kuralı), AccessKey ve en az 16 karakterlik SecretKey gerekli.")
            .ValidateOnStart();

        services.AddSingleton<IFileStorage>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<StorageOptions>>().Value;

            return options.Enabled ? new S3FileStorage(options) : new DisabledFileStorage();
        });

        return services;
    }
}
