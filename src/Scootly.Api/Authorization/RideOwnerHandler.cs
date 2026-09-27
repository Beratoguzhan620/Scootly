using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Scootly.Domain.Riding;

namespace Scootly.Api.Authorization;

/// <summary>
/// Kaynak tabanlı yetkilendirme: sürüş önce yüklenir, sonra bu handler'a kaynak olarak verilir
/// (ek veritabanı sorgusu yapılmaz).
/// </summary>
public sealed class RideOwnerHandler : AuthorizationHandler<RideOwnerRequirement, Ride>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RideOwnerRequirement requirement,
        Ride ride)
    {
        if (Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && ride.DriverId == userId)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
