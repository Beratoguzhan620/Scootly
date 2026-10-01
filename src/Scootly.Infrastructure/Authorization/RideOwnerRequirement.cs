using Microsoft.AspNetCore.Authorization;

namespace Scootly.Infrastructure.Authorization;

public sealed class RideOwnerRequirement : IAuthorizationRequirement
{
}