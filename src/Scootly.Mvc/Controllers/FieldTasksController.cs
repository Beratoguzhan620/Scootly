using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scootly.Application.Abstractions;
using Scootly.Application.FieldOps.Commands;
using Scootly.Infrastructure.Authorization;

namespace Scootly.Mvc.Controllers;

[Authorize(Policy = PolicyNames.FleetOperations)]
public sealed class FieldTasksController : Controller
{
    private readonly IFieldTaskReadService _readService;
    private readonly FieldTaskCommandHandler _handler;
    private readonly ICurrentUser _currentUser;

    public FieldTasksController(
        IFieldTaskReadService readService,
        FieldTaskCommandHandler handler,
        ICurrentUser currentUser)
    {
        _readService = readService;
        _handler = handler;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var tasks = await _readService.GetOpenTasksAsync(cancellationToken);
        return View(tasks);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(Guid id, CancellationToken cancellationToken)
    {
        var result = await _handler.Handle(new AssignFieldTaskCommand(id, _currentUser.UserId), cancellationToken);

        TempData["SuccessMessage"] = result.IsSuccess
            ? "Görev üstlenildi."
            : result.Error;

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(Guid id, string? note, CancellationToken cancellationToken)
    {
        var result = await _handler.Handle(new CompleteFieldTaskCommand(id, _currentUser.UserId, note), cancellationToken);

        TempData["SuccessMessage"] = result.IsSuccess
            ? "Görev tamamlandı."
            : result.Error;

        return RedirectToAction(nameof(Index));
    }
}