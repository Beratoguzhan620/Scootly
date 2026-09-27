using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Scootly.Application.Abstractions;
using Scootly.Infrastructure.Caching;
using Scootly.Infrastructure.Geo;
using Scootly.Infrastructure.HealthChecks;
using Scootly.Infrastructure.Identity;
using Scootly.Infrastructure.Messaging;
using Scootly.Infrastructure.Messaging.Idempotency;
using Scootly.Infrastructure.Messaging.Outbox;
using Scootly.Infrastructure.Payments;
using Scootly.Infrastructure.Persistence;
using Scootly.Infrastructure.Persistence.Repositories;
using Scootly.Infrastructure.Time;
using StackExchange.Redis;

namespace Scootly.Infrastructure;

public static class InfrastructureDependencyInjection
{
    public const string ConnectionStringName = "DefaultConnection";

    /// <summary>Kalıcılık, önbellek, bölge çözümleme ve mesajlaşma altyapısı (Api ve Worker ortak).</summary>
    public static IServiceCollection AddScootlyInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ScootlyDbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"'ConnectionStrings:{ConnectionStringName}' yapılandırılmamış. Geliştirmede user-secrets, " +
                    "diğer ortamlarda 'ConnectionStrings__DefaultConnection' ortam değişkeni kullanın.");
            }

            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ScootlyDbContext>());
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ScootlyDbContext>());

        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IRideRepository, RideRepository>();
        services.AddScoped<ITelemetryRepository, TelemetryRepository>();
        services.AddScoped<IServiceAreaRepository, ServiceAreaRepository>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddMemoryCache();
        services.AddScoped<IRegionResolver, ServiceAreaRegionResolver>();

        AddCaching(services);
        AddMessaging(services, configuration);

        return services;
    }

    public static bool IsMessagingEnabled(IConfiguration configuration)
        => configuration.GetValue($"{MessagingOptions.SectionName}:{nameof(MessagingOptions.Enabled)}", defaultValue: true);

    /// <summary>ASP.NET Core Identity (çerez şemaları olmadan) ve token servisleri.</summary>
    public static IServiceCollection AddScootlyIdentity(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<DeviceAuthOptions>().BindConfiguration(DeviceAuthOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<BootstrapOptions>().BindConfiguration(BootstrapOptions.SectionName);

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ScootlyDbContext>()
            .AddSignInManager();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUserAccessor>();
        services.AddScoped<JwtTokenGenerator>();
        services.AddScoped<DeviceTokenService>();
        services.AddHostedService<IdentityBootstrapper>();

        return services;
    }

    public static IServiceCollection AddScootlyPaymentGateway(this IServiceCollection services)
    {
        services.AddOptions<PaymentGatewayOptions>().BindConfiguration(PaymentGatewayOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();

        services
            .AddHttpClient<IPaymentGateway, PaymentSimulatorClient>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<PaymentGatewayOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                // Zaman aşımını dayanıklılık hattı yönetir; HttpClient'ın kendi sınırı onun üzerinde kalmalı.
                client.Timeout = TimeSpan.FromSeconds(options.TotalTimeoutSeconds + 5);
            })
            .AddPaymentResilience();

        return services;
    }

    public static IServiceCollection AddScootlyPaymentWebhooks(this IServiceCollection services)
    {
        services.AddOptions<PaymentWebhookOptions>().BindConfiguration(PaymentWebhookOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddScoped<PaymentWebhookValidator>();

        return services;
    }

    public static IHealthChecksBuilder AddScootlyHealthChecks(this IHealthChecksBuilder builder, IConfiguration configuration)
    {
        builder
            .AddDbContextCheck<ScootlyDbContext>("postgres", tags: ["ready"])
            .AddCheck<RedisHealthCheck>("redis", tags: ["ready"]);

        if (IsMessagingEnabled(configuration))
            builder.AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["ready"]);

        return builder;
    }

    private static void AddCaching(IServiceCollection services)
    {
        services.AddOptions<RedisOptions>().BindConfiguration(RedisOptions.SectionName);

        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<RedisOptions>>().Value;
            var configuration = ConfigurationOptions.Parse(options.ConnectionString!);
            configuration.AbortOnConnectFail = false;

            return ConnectionMultiplexer.Connect(configuration);
        });

        services.AddScoped<ICacheService>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<RedisOptions>>().Value;

            return string.IsNullOrWhiteSpace(options.ConnectionString)
                ? new InMemoryCacheService(serviceProvider.GetRequiredService<IMemoryCache>())
                : new RedisCacheService(serviceProvider.GetRequiredService<IConnectionMultiplexer>());
        });

        services.AddScoped<NearbyVehicleCache>();
    }

    private static void AddMessaging(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MessagingOptions>().BindConfiguration(MessagingOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();

        var rabbitMqOptions = services.AddOptions<RabbitMqOptions>()
            .BindConfiguration(RabbitMqOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddScoped<OutboxProcessor>();
        services.AddScoped<IdempotentMessageHandler>();

        if (!IsMessagingEnabled(configuration))
            return;

        rabbitMqOptions.ValidateOnStart();
        services.AddHostedService<OutboxPublisherService>();
    }
}
