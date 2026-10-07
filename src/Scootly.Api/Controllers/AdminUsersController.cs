using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scootly.Api.ErrorHandling;
using Scootly.Api.Extensions;
using Scootly.Infrastructure.Authorization;
using Scootly.Infrastructure.Identity;

namespace Scootly.Api.Controllers;

/// <summary>
/// Kullanıcı rol yönetimi (yalnızca filo yöneticileri). Her değişiklik kullanıcının güvenlik damgasını yeniler:
/// eski rolleri taşıyan token'lar artık kabul edilmez, kullanıcı yeni rolleriyle yeniden giriş yapar.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/users")]
[Authorize(Policy = PolicyNames.FleetManagerOnly)]
[EnableRateLimiting(RateLimitPolicies.User)]
public sealed class AdminUsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly UserSessionValidator _sessionValidator;
    private readonly ILogger<AdminUsersController> _logger;

    public AdminUsersController(
        UserManager<ApplicationUser> userManager,
        UserSessionValidator sessionValidator,
        ILogger<AdminUsersController> logger)
    {
        _userManager = userManager;
        _sessionValidator = sessionValidator;
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
            EnsureSucceeded(await _userManager.AddToRoleAsync(user, role), "rol atanamadı");
            await RevokeSessionsAsync(user);
            _logger.LogInformation("Rol atandı: {Role} → {UserId} (işlemi yapan {ActorId})", role, userId, ActorId);
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

        if (role == ScootlyRoles.FleetManager && ActorId == userId.ToString())
            return this.BadRequestProblem("Kendi filo yöneticisi rolünüzü kaldıramazsınız.");

        if (await _userManager.IsInRoleAsync(user, role))
        {
            EnsureSucceeded(await _userManager.RemoveFromRoleAsync(user, role), "rol kaldırılamadı");
            await RevokeSessionsAsync(user);
            _logger.LogInformation("Rol kaldırıldı: {Role} ← {UserId} (işlemi yapan {ActorId})", role, userId, ActorId);
        }

        return NoContent();
    }

    private string? ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private async Task RevokeSessionsAsync(ApplicationUser user)
    {
        EnsureSucceeded(await _userManager.UpdateSecurityStampAsync(user), "güvenlik damgası yenilenemedi");
        _sessionValidator.Invalidate(user.Id);
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"Kullanıcı güncellenemedi ({operation}): {string.Join("; ", result.Errors.Select(e => e.Code))}");
    }
}
