using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Scootly.Infrastructure.Identity;

public sealed class ScootlyUserClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<ApplicationUser, ApplicationRole>
{
    public ScootlyUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim(ScootlyClaimTypes.ClientType, ScootlyClaimTypes.UserClient));

        return identity;
    }
}
