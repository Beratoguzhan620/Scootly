using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Scootly.Infrastructure.Identity;

namespace Scootly.Mvc.Controllers;

[Authorize]
public sealed class MapController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtTokenGenerator _tokenGenerator;

    public MapController(UserManager<ApplicationUser> userManager, JwtTokenGenerator tokenGenerator)
    {
        _userManager = userManager;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user is null)
            return Challenge();

        var roles = await _userManager.GetRolesAsync(user);
        var token = _tokenGenerator.GenerateUserToken(user, roles, TimeSpan.FromMinutes(15));

        ViewBag.HubToken = token;

        return View();
    }
}