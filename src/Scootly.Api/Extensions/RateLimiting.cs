using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Scootly.Api.Extensions;

public static class RateLimitPolicies
{
    /// <summary>Anonim okuma uçları: IP başına.</summary>
    public const string Anonymous = "Anonymous";

    /// <summary>Giriş/kayıt/cihaz token uçları: kaba kuvvet denemelerine karşı IP başına sıkı sınır.</summary>
    public const string Auth = "Auth";

    /// <summary>Oturum açmış kullanıcının yazma işlemleri: kullanıcı başına.</summary>
    public const string User = "User";

    /// <summary>Telemetri: cihaz istemcisi başına.</summary>
    public const string Device = "Device";

    /// <summary>Ödeme sağlayıcısı webhook'ları: IP başına.</summary>
    public const string Webhook = "Webhook";
}

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int AnonymousPerMinute { get; init; } = 60;
    public int AuthPerMinute { get; init; } = 10;
    public int UserPerMinute { get; init; } = 30;
    public int DevicePerMinute { get; init; } = 600;
    public int WebhookPerMinute { get; init; } = 300;
}

public static class RateLimitingExtensions
{
    /// <summary>
    /// Sınırlar istemci başına bölümlenir (partitioned): tek bir istemcinin trafiği diğerlerini engelleyemez.
    /// Değerler çalışma anında okunur, bu yüzden yapılandırma değişikliği yeniden başlatma gerektirmez.
    /// </summary>
    public static IServiceCollection AddScootlyRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitingOptions>().BindConfiguration(RateLimitingOptions.SectionName);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejectionAsync;

            options.AddPolicy(RateLimitPolicies.Anonymous, context =>
                FixedWindowPerMinute($"ip:{ClientIp(context)}", Limits(context).AnonymousPerMinute));

            options.AddPolicy(RateLimitPolicies.Auth, context =>
                FixedWindowPerMinute($"ip:{ClientIp(context)}", Limits(context).AuthPerMinute));

            options.AddPolicy(RateLimitPolicies.User, context =>
                FixedWindowPerMinute($"user:{context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ClientIp(context)}", Limits(context).UserPerMinute));

            options.AddPolicy(RateLimitPolicies.Device, context =>
                FixedWindowPerMinute($"device:{context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ClientIp(context)}", Limits(context).DevicePerMinute));

            options.AddPolicy(RateLimitPolicies.Webhook, context =>
                FixedWindowPerMinute($"ip:{ClientIp(context)}", Limits(context).WebhookPerMinute));
        });

        return services;
    }

    private static RateLimitingOptions Limits(HttpContext context)
        => context.RequestServices.GetRequiredService<IOptionsMonitor<RateLimitingOptions>>().CurrentValue;

    private static string ClientIp(HttpContext context)
        => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static RateLimitPartition<string> FixedWindowPerMinute(string partitionKey, int permitLimit)
        => RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, permitLimit),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        var problemDetailsService = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Çok fazla istek",
                Detail = "İstek sınırı aşıldı. Lütfen daha sonra tekrar deneyin."
            }
        });
    }
}
