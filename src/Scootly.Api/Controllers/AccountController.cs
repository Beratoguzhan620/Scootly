using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scootly.Api.Contracts.Requests;
using Scootly.Api.Contracts.Responses;
using Scootly.Api.ErrorHandling;
using Scootly.Api.Extensions;
using Scootly.Api.Validators;
using Scootly.Application.Abstractions;
using Scootly.Infrastructure.Authorization;
using Scootly.Infrastructure.Identity;

namespace Scootly.Api.Controllers;

/// <summary>
/// Oturum açmış kullanıcının kendi hesabı: bilgileri görme, parola değiştirme ve hesabı silme (KVKK silme hakkı).
/// Parola sıfırlama ve e-posta doğrulama bir e-posta sağlayıcısı gerektirdiği için bu sürümde yoktur.
/// </summary>
[ApiController]
[Route("api/account")]
[Authorize(Policy = PolicyNames.UserOnly)]
[EnableRateLimiting(RateLimitPolicies.User)]
public sealed class AccountController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUser _currentUser;

    public AccountController(UserManager<ApplicationUser> userManager, ICurrentUser currentUser)
    {
        _userManager = userManager;
        _currentUser = currentUser;
    }

    [HttpGet]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get()
    {
        var user = await _userManager.FindByIdAsync(_currentUser.UserId.ToString());

        if (user is null)
            return NotFound();

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new AccountResponse(user.Id, user.Email ?? string.Empty, roles.Order(StringComparer.Ordinal).ToList()));
    }

    /// <summary>Parola değişince eski token'lar geçersiz olur; yanıt, yeni parolayla verilmiş taze bir token içerir.</summary>
    [HttpPost("change-password")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        [FromServices] JwtTokenGenerator tokenGenerator,
        [FromServices] UserSessionValidator sessionValidator)
    {
        if (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword)
            || request.NewPassword.Length > CredentialsValidator.PasswordMaxLength)
        {
            return this.BadRequestProblem($"Mevcut ve yeni parola zorunludur (en fazla {CredentialsValidator.PasswordMaxLength} karakter).");
        }

        var user = await _userManager.FindByIdAsync(_currentUser.UserId.ToString());

        if (user is null)
            return NotFound();

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

        if (!result.Succeeded)
        {
            var message = result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch))
                ? "Mevcut parola hatalı."
                : string.Join(" ", result.Errors.Select(e => e.Description));

            return this.BadRequestProblem(message);
        }

        sessionValidator.Invalidate(user.Id);

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new TokenResponse(tokenGenerator.GenerateUserToken(user, roles)));
    }

    /// <summary>Hesabı kalıcı olarak siler ve sürüş konumlarını anonimleştirir. Açık rezervasyon, sürüş veya borç varken 409.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        [FromBody] DeleteAccountRequest request,
        [FromServices] AccountDeletionService deletionService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length > CredentialsValidator.PasswordMaxLength)
            return this.BadRequestProblem("Hesabı silmek için parolanızı girin.");

        var result = await deletionService.DeleteAsync(_currentUser.UserId, request.Password, cancellationToken);

        return result.IsSuccess ? NoContent() : this.ToProblem(result);
    }
}
