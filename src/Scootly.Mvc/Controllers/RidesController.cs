using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scootly.Application.Abstractions;

namespace Scootly.Mvc.Controllers;

[Authorize]
public sealed class RidesController : Controller
{
    private readonly IRideReadService _rideReadService;
    private readonly ICurrentUser _currentUser;

    public RidesController(IRideReadService rideReadService, ICurrentUser currentUser)
    {
        _rideReadService = rideReadService;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var rides = await _rideReadService.GetActiveRidesForDriverAsync(_currentUser.UserId, cancellationToken);
        return View(rides);
    }
}
