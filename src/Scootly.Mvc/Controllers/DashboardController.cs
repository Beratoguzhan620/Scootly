using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Scootly.Mvc.Controllers;

[Authorize]
public sealed class DashboardController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
