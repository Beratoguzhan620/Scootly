using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scootly.Api.Contracts.Requests;
using Scootly.Api.Contracts.Responses;
using Scootly.Api.ErrorHandling;
using Scootly.Api.Extensions;
using Scootly.Api.Validators;
using Scootly.Infrastructure.Identity;

namespace Scootly.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class AuthController : ControllerBase
{
    private const string GenericRegistrationError = "Kayıt tamamlanamadı. Bilgilerinizi kontrol edip tekrar deneyin.";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly JwtTokenGenerator _tokenGenerator;
    private readonly CredentialsValidator _credentialsValidator;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        JwtTokenGenerator tokenGenerator,
        CredentialsValidator credentialsValidator,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenGenerator = tokenGenerator;
        _credentialsValidator = credentialsValidator;
        _logger = logger;
    }

    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var validation = _credentialsValidator.Validate(request.Email, request.Password);

        if (!validation.IsValid)
            return this.BadRequestProblem(validation.Error!);

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            // "Bu e-posta zaten kayıtlı" gibi hatalar hesap varlığını sızdırır; parola kuralı hataları ise kullanıcıya gösterilir.
            var errors = result.Errors
                .Where(e => e.Code.StartsWith("Password", StringComparison.Ordinal))
                .Select(e => e.Description)
                .ToList();

            return this.BadRequestProblem(errors.Count > 0 ? string.Join(" ", errors) : GenericRegistrationError);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, ScootlyRoles.Driver);

        if (!roleResult.Succeeded)
        {
            // Rolsüz yarım hesap bırakılmaz.
            await _userManager.DeleteAsync(user);
            throw new InvalidOperationException(
                $"Yeni kullanıcıya {ScootlyRoles.Driver} rolü atanamadı: {string.Join("; ", roleResult.Errors.Select(e => e.Code))}");
        }

        return StatusCode(StatusCodes.Status201Created);
    }

    [HttpPost("login")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var validation = _credentialsValidator.Validate(request.Email, request.Password);

        if (!validation.IsValid)
            return Unauthorized();

        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user is null)
        {
            // Var olmayan hesapta da parola özeti hesaplanır: yanıt süresi hesabın varlığını ele vermez.
            _userManager.PasswordHasher.HashPassword(new ApplicationUser(), request.Password);
            return Unauthorized();
        }

        var signIn = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (signIn.IsLockedOut)
            _logger.LogWarning("Hesap çok sayıda başarısız giriş nedeniyle kilitli: {UserId}", user.Id);

        if (!signIn.Succeeded)
            return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new TokenResponse(_tokenGenerator.GenerateUserToken(user, roles)));
    }
}
