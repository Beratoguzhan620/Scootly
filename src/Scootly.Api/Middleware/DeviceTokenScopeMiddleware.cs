using System.Security.Claims;
using Scootly.Infrastructure.Identity;

namespace Scootly.Api.Middleware;

/// <summary>
/// Cihaz token'ının yalnızca cihaz uçlarına ulaşmasını sağlar.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sıra kritik.</b> Bu ara katman <c>UseAuthentication</c>'DAN SONRA,
/// <c>UseAuthorization</c>'DAN ÖNCE çalışmalı. Önce çalışsaydı
/// <c>context.User</c> henüz doldurulmamış olurdu ve her isteği "cihaz değil"
/// sayardı — yani hiçbir şeyi engellemezdi ve bunu fark etmezdik, çünkü
/// başarısızlığı sessiz olurdu. Sonra çalışsaydı, yetki kararı zaten verilmiş
/// olurdu.
/// </para>
/// <para>
/// Cihaz token'ının hedef kitlesi (audience) zaten kullanıcı token'ından farklı,
/// yani bir cihaz token'ı kullanıcı uçlarında doğrulanamaz. Bu ara katman ikinci
/// bir savunma hattı: audience yapılandırması ileride yanlışlıkla gevşetilirse,
/// yol kısıtı hâlâ yerinde durur.
/// </para>
/// </remarks>
public sealed class DeviceTokenScopeMiddleware
{
    /// <summary>Cihaz token'ının gidebileceği tek yol öneki.</summary>
    private const string CihazYolOneki = "/api/v1/devices";

    /// <summary>Cihazın token aldığı uç — kimliksiz erişilir, burada kontrol edilmez.</summary>
    private const string CihazTokenYolu = "/api/v1/device-auth";

    private readonly RequestDelegate _next;
    private readonly ILogger<DeviceTokenScopeMiddleware> _logger;

    public DeviceTokenScopeMiddleware(RequestDelegate next, ILogger<DeviceTokenScopeMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var cihazKimligi = context.User.FindFirstValue(ScootlyClaimTypes.DeviceId);

        if (!string.IsNullOrEmpty(cihazKimligi))
        {
            var yol = context.Request.Path.Value ?? string.Empty;

            // OrdinalIgnoreCase — kültüre duyarlı karşılaştırma değil.
            // Türkçe yerel ayarında "I" harfi noktasız "ı"ya dönüşür ve
            // yol karşılaştırması makinenin dil ayarına göre değişirdi.
            var izinli = yol.StartsWith(CihazYolOneki, StringComparison.OrdinalIgnoreCase)
                         || yol.StartsWith(CihazTokenYolu, StringComparison.OrdinalIgnoreCase);

            if (!izinli)
            {
                _logger.LogWarning(
                    "Cihaz token'ı cihaz dışı bir yola gitmeye çalıştı. Cihaz: {DeviceId}, Yol: {Path}",
                    cihazKimligi, yol);

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        await _next(context);
    }
}
