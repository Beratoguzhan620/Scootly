using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scootly.Api.Authorization;
using Scootly.Api.ErrorHandling;
using Scootly.Api.Extensions;
using Scootly.Infrastructure.Identity;

namespace Scootly.Api.Controllers;

/// <summary>Kullanıcı rol yönetimi (yalnızca filo yöneticileri).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/users")]
[Authorize(Policy = PolicyNames.FleetManagerOnly)]
[EnableRateLimiting(RateLimitPolicies.User)]
public sealed class AdminUsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AdminUsersController> _logger;

    public AdminUsersController(UserManager<ApplicationUser> userManager, ILogger<AdminUsersController> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    [HttpPost("{userId:guid}/roles/{role}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignRole(Guid userId, string role)
    {
        if (!ScootlyRoles.Assignable.Contains(role))
            return this.BadRequestProblem($"Geçersiz rol. Geçerli roller: {string.Join(", ", ScootlyRoles.Assignable)}.");

        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null)
            return NotFound();

        if (!await _userManager.IsInRoleAsync(user, role))
        {
            await _userManager.AddToRoleAsync(user, role);
            _logger.LogInformation("Rol atandı: {Role} → {UserId}", role, userId);
        }

        return NoContent();
    }

    [HttpDelete("{userId:guid}/roles/{role}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveRole(Guid userId, string role)
    {
        if (!ScootlyRoles.Assignable.Contains(role))
            return this.BadRequestProblem($"Geçersiz rol. Geçerli roller: {string.Join(", ", ScootlyRoles.Assignable)}.");

        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null)
            return NotFound();

        if (role == ScootlyRoles.FleetManager && User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value == userId.ToString())
            return this.BadRequestProblem("Kendi filo yöneticisi rolünüzü kaldıramazsınız.");

        if (await _userManager.IsInRoleAsync(user, role))
        {
            await _userManager.RemoveFromRoleAsync(user, role);
            _logger.LogInformation("Rol kaldırıldı: {Role} ← {UserId}", role, userId);
        }

        return NoContent();
    }
}
