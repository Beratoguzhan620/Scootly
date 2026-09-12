using System.Security.Claims;
using Scootly.Application.Abstractions;
using Scootly.Infrastructure.Identity;

namespace Scootly.Api.Identity;

/// <summary>
/// <see cref="ICurrentUser"/>'ı, isteğin doğrulanmış token'ından doldurur.
/// </summary>
/// <remarks>
/// <para>
/// Bu sınıf Api katmanında duruyor, Infrastructure'da değil. Gerekçe: okuduğu
/// şey bir HTTP isteğidir. Infrastructure kalıcılık ve dış sistem katmanıdır;
/// oraya <c>HttpContext</c> bağımlılığı taşımak, veritabanı kodunun bir web
/// isteğinin varlığını varsaymaya başlamasına açılan kapıdır.
/// </para>
/// <para>
/// Kimlik yalnızca token'dan okunur. İstek gövdesinden kullanıcı kimliği almak,
/// herkesin başkası adına işlem yapabilmesi demektir (OWASP A01 — yetkisiz nesne
/// erişimi). 26. gündeki tarama bunu tam olarak arayacak.
/// </para>
/// </remarks>
public sealed class CurrentUserAccessor : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// İsteği yapan kullanıcının kimliği. Kimlik doğrulanmamışsa
    /// <see cref="Guid.Empty"/> döner — bu değer hiçbir gerçek kaydın sahibiyle
    /// eşleşmediği için sahiplik kontrolleri kapalı tarafta hata verir.
    /// </summary>
    public Guid UserId
    {
        get
        {
            var raw = Principal?.FindFirstValue(ScootlyClaimTypes.Subject);
            return Guid.TryParse(raw, out var userId) ? userId : Guid.Empty;
        }
    }

    /// <summary>
    /// Kullanıcının rolü. Birden fazla rol varsa ilki döner;
    /// çoklu rol kararları <c>[Authorize(Roles = ...)]</c> ve politikalar
    /// üzerinden verilir (23. gün), bu alan üzerinden değil.
    /// </summary>
    public string Role => Principal?.FindFirstValue(ScootlyClaimTypes.Role) ?? string.Empty;

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;
}
