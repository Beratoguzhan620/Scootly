using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Scootly.Api.Contracts.Requests;
using Scootly.Api.Contracts.Responses;
using Scootly.Infrastructure.Identity;

namespace Scootly.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    /// <summary>
    /// Giriş başarısız olduğunda dönen TEK mesaj.
    /// </summary>
    /// <remarks>
    /// "Kullanıcı bulunamadı" ile "parola hatalı" ayrı ayrı söylenirse, saldırgan
    /// bir e-posta listesini deneyerek hangilerinin kayıtlı olduğunu öğrenir
    /// (kullanıcı sayımı / enumeration). Hesabın kilitli olduğunu söylemek de aynı
    /// bilgiyi verir. Bu yüzden üç durum da aynı yanıtı döner.
    /// </remarks>
    private const string GirisBasarisizDetay = "E-posta veya parola hatalı.";

    // Kullanıcı hiç bulunamadığında da bir hash doğrulaması çalıştırılır.
    // Aksi halde "kullanıcı yok" yanıtı belirgin biçimde daha hızlı döner ve
    // yanıt süresi, mesaj gizlense bile aynı bilgiyi sızdırır (zamanlama yan kanalı).
    private static readonly ApplicationUser ZamanDengeleyiciKullanici = new()
    {
        UserName = "bulunmayan@scootly.local"
    };

    private static readonly string ZamanDengeleyiciHash =
        new PasswordHasher<ApplicationUser>()
            .HashPassword(ZamanDengeleyiciKullanici, "bu-deger-hicbir-hesaba-ait-degil");

    private readonly UserManager<ApplicationUser> _users;
    private readonly IPasswordHasher<ApplicationUser> _hasher;
    private readonly JwtTokenGenerator _tokens;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> users,
        IPasswordHasher<ApplicationUser> hasher,
        JwtTokenGenerator tokens,
        ILogger<AuthController> logger)
    {
        _users = users;
        _hasher = hasher;
        _tokens = tokens;
        _logger = logger;
    }

    /// <summary>Yeni sürücü kaydı. Rol istemciden alınmaz; her kayıt Driver olur.</summary>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email
        };

        var created = await _users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            // Identity'nin hata AÇIKLAMALARI kullanıcıya gösterilebilir: parola
            // politikası zaten herkese açık bir kural. Sızdırdığı tek ek bilgi
            // e-postanın kayıtlı olduğudur; bu, e-posta doğrulaması eklenmeden
            // kaçınılamayan bir ödünleşme (bkz. ADR 0005).
            return BadRequest(new ApiErrorResponse(
                "Kayıt başarısız",
                string.Join(" ", created.Errors.Select(error => error.Description)),
                StatusCodes.Status400BadRequest));
        }

        var roleAssigned = await _users.AddToRoleAsync(user, RoleNames.Driver);
        if (!roleAssigned.Succeeded)
        {
            // Rolsüz bir hesap bırakmak, sonradan teşhisi zor bir yetki sorunudur.
            await _users.DeleteAsync(user);
            return BadRequest(new ApiErrorResponse(
                "Kayıt başarısız",
                "Kullanıcı oluşturuldu ancak rol atanamadı; işlem geri alındı.",
                StatusCodes.Status400BadRequest));
        }

        return StatusCode(StatusCodes.Status201Created);
    }

    /// <summary>E-posta ve parola ile giriş; başarılıysa imzalı token döner.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _users.FindByEmailAsync(request.Email);

        if (user is null)
        {
            _hasher.VerifyHashedPassword(ZamanDengeleyiciKullanici, ZamanDengeleyiciHash, request.Password);
            return GirisBasarisiz();
        }

        if (await _users.IsLockedOutAsync(user))
        {
            // Kullanıcıya kilitli olduğunu söylemiyoruz ama kendi kaydımıza yazıyoruz:
            // aynı hesaba art arda kilit gelmesi, saldırı göstergesidir.
            // E-posta değil kimlik loglanıyor (bkz. 27. gün, log gizliliği).
            _logger.LogWarning("Kilitli hesaba giriş denemesi. Kullanıcı: {UserId}", user.Id);
            return GirisBasarisiz();
        }

        if (!await _users.CheckPasswordAsync(user, request.Password))
        {
            // 21. günde tanımlanan kilit politikasını asıl çalıştıran satır bu.
            // Bu çağrı olmadan MaxFailedAccessAttempts yalnızca kâğıt üstünde kalır.
            await _users.AccessFailedAsync(user);
            return GirisBasarisiz();
        }

        await _users.ResetAccessFailedCountAsync(user);

        var roles = await _users.GetRolesAsync(user);
        var token = _tokens.GenerateToken(user, roles);

        return Ok(new TokenResponse(token, _tokens.ExpiresAt()));
    }

    private IActionResult GirisBasarisiz() =>
        Unauthorized(new ApiErrorResponse(
            "Giriş başarısız",
            GirisBasarisizDetay,
            StatusCodes.Status401Unauthorized));
}
