using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Scootly.Mvc.Controllers;

[Authorize]
public sealed class MapController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}