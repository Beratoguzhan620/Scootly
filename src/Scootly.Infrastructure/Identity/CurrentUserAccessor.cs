using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Scootly.Application.Abstractions;

namespace Scootly.Infrastructure.Identity;

public sealed class CurrentUserAccessor : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsAuthenticated => TryGetUserId(out _);

    public Guid UserId => TryGetUserId(out var userId)
        ? userId
        : throw new UnauthorizedAccessException("İstek, geçerli bir kullanıcı kimliği taşımıyor.");

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
            return false;

        // Cihaz token'larının kimliği bir kullanıcı kimliği değildir.
        if (user.FindFirstValue(ScootlyClaimTypes.ClientType) != ScootlyClaimTypes.UserClient)
            return false;

        return Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out userId) && userId != Guid.Empty;
    }
}
