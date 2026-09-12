using Microsoft.AspNetCore.Identity;

namespace Scootly.Infrastructure.Identity;

/// <summary>
/// Scootly rolü. Ek alan taşımaz; yalnızca anahtar tipini
/// <see cref="ApplicationUser"/> ile aynı hizaya çekmek için türetilmiştir.
/// </summary>
public sealed class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string roleName) : base(roleName)
    {
    }
}
