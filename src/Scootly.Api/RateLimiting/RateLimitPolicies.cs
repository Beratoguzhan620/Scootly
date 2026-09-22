using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Scootly.Infrastructure.Identity;

namespace Scootly.Api.RateLimiting;

/// <summary>
/// İstek oranı sınırları (55. gün).
/// </summary>
/// <remarks>
/// <para>
/// <b>İki ayrı politika olmasının sebebi somut.</b> 53. günde başlatılan 200
/// sanal araç beş saniyede bir telemetri gönderiyor. Anonim harita trafiğiyle
/// aynı limite tabi olsalardı (dakikada 60), normal çalışan bir filo kendi
/// kendini "aşırı istek" diye engellerdi — yani koruma, koruduğu sistemi
/// durdururdu.
/// </para>
/// <para>
/// <b>Bölümleme anahtarı da farklı.</b> Anonim trafik IP'ye göre bölümleniyor,
/// çünkü elde başka bir kimlik yok. Cihaz trafiği cihaz kimliğine göre
/// bölümleniyor, çünkü bir filodaki 200 cihaz aynı NAT arkasından tek IP ile
/// çıkabilir: IP'ye göre sınırlansalardı hepsi tek bir kotayı paylaşır ve
/// filo büyüdükçe erken engellenirdi.
/// </para>
/// <para>
/// <b>Kayan pencere (sliding window) seçildi, sabit pencere değil.</b> Sabit
/// pencerede iki pencerenin sınırına yığılan istekler limitin iki katını tek
/// bir saniyede geçirebilir ("sınır patlaması"). Kayan pencere, pencereyi
/// dilimlere bölerek bunu yumuşatıyor.
/// </para>
/// </remarks>
public static class RateLimitPolicies
{
    /// <summary>Anonim harita sorguları — IP başına.</summary>
    public const string AnonimHarita = "anonim-harita";

    /// <summary>Cihaz telemetrisi — cihaz kimliği başına.</summary>
    public const string CihazTelemetri = "cihaz-telemetri";

    private const int AnonimDakikalikLimit = 60;
    private const int CihazDakikalikLimit = 600;
    private const int PencereDilimi = 6;

    public static IServiceCollection AddScootlyRateLimiting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddRateLimiter(options =>
        {
            // Varsayılan 503'tür ve yanlıştır: 503 "sunucu şu an hizmet
            // veremiyor" demek, oysa burada sunucu gayet iyi durumda ve
            // İSTEMCİ çok fazla istek gönderiyor. Doğru kod 429.
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                // Retry-After başlığı olmadan istemci ne zaman tekrar
                // deneyeceğini bilemez ve genellikle hemen tekrar dener —
                // yani sınırlama, yükü azaltmak yerine artırır.
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var yenidenDene))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)yenidenDene.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";

                await context.HttpContext.Response.WriteAsync(
                    "Çok fazla istek gönderildi. Lütfen biraz bekleyin.", cancellationToken);
            };

            options.AddPolicy(AnonimHarita, httpContext =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: IpAnahtari(httpContext),
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = AnonimDakikalikLimit,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = PencereDilimi,

                        // Kuyruk YOK. Sınırı aşan istek beklemek yerine hemen
                        // reddediliyor: bekleyen istek bir iş parçacığı ve bir
                        // bağlantı tutar, yani saldırgan tam da tüketmek
                        // istediği kaynağı tüketmeye devam ederdi.
                        QueueLimit = 0
                    }));

            options.AddPolicy(CihazTelemetri, httpContext =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: CihazAnahtari(httpContext),
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = CihazDakikalikLimit,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = PencereDilimi,
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    private static string IpAnahtari(HttpContext httpContext)
    {
        // RemoteIpAddress ters vekil (reverse proxy) arkasında vekilin
        // adresini verir; gerçek istemci için ForwardedHeaders ara katmanı
        // gerekiyor. Bu, 20. haftada Nginx kurulurken yapılacak — şimdi
        // yapılsaydı, doğrulanmamış bir X-Forwarded-For başlığına güvenmek
        // olurdu ve sınırlamayı istemcinin kendisi atlatabilirdi.
        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "bilinmeyen";
    }

    private static string CihazAnahtari(HttpContext httpContext)
    {
        // Kimliği token'dan okuyoruz, gövdeden veya başlıktan değil:
        // istemcinin etkileyebildiği bir bölümleme anahtarı, istemcinin
        // kendine sınırsız kota açabilmesi demektir.
        return httpContext.User.FindFirstValue(ScootlyClaimTypes.DeviceId) ?? IpAnahtari(httpContext);
    }
}
