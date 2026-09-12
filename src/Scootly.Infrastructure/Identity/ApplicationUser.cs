using Microsoft.AspNetCore.Identity;

namespace Scootly.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? HomeRegion { get; set; }
}