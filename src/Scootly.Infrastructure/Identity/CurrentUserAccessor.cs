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

    public Guid UserId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return value is not null ? Guid.Parse(value) : Guid.Empty;
        }
    }

    public string Role
    {
        get
        {
            return _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        }
    }
}