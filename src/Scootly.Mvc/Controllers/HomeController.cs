using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scootly.Mvc.Models;

namespace Scootly.Mvc.Controllers;

[AllowAnonymous]
public sealed class HomeController : Controller
{
    /// <summary>Giriş yapmış kullanıcı doğrudan panele gider; diğerleri tanıtım ve giriş bağlantısını görür.</summary>
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? statusCode = null)
    {
        if (statusCode.HasValue)
            Response.StatusCode = statusCode.Value;

        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            StatusCode = statusCode
        });
    }
}
