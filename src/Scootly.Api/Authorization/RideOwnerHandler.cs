using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Scootly.Application.Abstractions;

namespace Scootly.Api.Authorization;

public sealed class RideOwnerHandler : AuthorizationHandler<RideOwnerRequirement, Guid>
{
    private readonly IApplicationDbContext _dbContext;

    public RideOwnerHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RideOwnerRequirement requirement,
        Guid rideId)
    {
        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userIdValue is null || !Guid.TryParse(userIdValue, out var userId))
            return Task.CompletedTask;

        var ride = _dbContext.Rides.FirstOrDefault(r => r.Id == rideId);

        if (ride is not null && ride.DriverId == userId)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}