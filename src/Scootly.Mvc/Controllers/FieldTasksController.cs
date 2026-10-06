using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scootly.Application.Abstractions;
using Scootly.Application.FieldOps;
using Scootly.Application.FieldOps.Commands;
using Scootly.Mvc.Models;
using Scootly.Infrastructure.Authorization;

namespace Scootly.Mvc.Controllers;

[Authorize(Policy = PolicyNames.FleetOperations)]
public sealed class FieldTasksController : Controller
{
    private readonly IFieldTaskReadService _readService;
    private readonly FieldTaskCommandHandler _handler;
    private const int RecentCompletedCount = 20;

    // Fotograf siniri + form alanlari icin 1 MB pay.
    private const int MaxRequestBytes = FieldTaskPhotoRules.MaxBytes + (1024 * 1024);

    private static readonly TimeSpan PhotoLinkLifetime = TimeSpan.FromSeconds(60);

    private readonly ICurrentUser _currentUser;
    private readonly IFileStorage _fileStorage;

    public FieldTasksController(
        IFieldTaskReadService readService,
        FieldTaskCommandHandler handler,
        ICurrentUser currentUser,
        IFileStorage fileStorage)
    {
        _readService = readService;
        _handler = handler;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var open = await _readService.GetOpenTasksAsync(cancellationToken);
        var completed = await _readService.GetRecentCompletedTasksAsync(RecentCompletedCount, cancellationToken);

        return View(new FieldTasksIndexViewModel(open, completed, _fileStorage.IsEnabled));
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
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> Complete(Guid id, string? note, IFormFile? photo, CancellationToken cancellationToken)
    {
        byte[]? photoContent = null;

        if (photo is not null && photo.Length > 0)
        {
            if (photo.Length > FieldTaskPhotoRules.MaxBytes)
            {
                TempData["SuccessMessage"] = "Foto\u011fraf 5 MB'dan b\u00fcy\u00fck olamaz.";
                return RedirectToAction(nameof(Index));
            }

            using var buffer = new MemoryStream((int)photo.Length);
            await photo.CopyToAsync(buffer, cancellationToken);
            photoContent = buffer.ToArray();
        }

        var result = await _handler.Handle(new CompleteFieldTaskCommand(id, _currentUser.UserId, note, photoContent), cancellationToken);

        TempData["SuccessMessage"] = result.IsSuccess
            ? "Görev tamamlandı."
            : result.Error;

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Photo(Guid id, CancellationToken cancellationToken)
    {
        if (!_fileStorage.IsEnabled)
            return NotFound();

        var objectKey = await _readService.GetPhotoObjectKeyAsync(id, cancellationToken);

        if (string.IsNullOrEmpty(objectKey))
            return NotFound();

        Response.Headers.CacheControl = "no-store";

        // Kisa omurlu on-imzali adres: tarayici fotografi dogrudan depodan alir, uygulama araci olmaz.
        return Redirect(_fileStorage.CreateDownloadUrl(objectKey, PhotoLinkLifetime));
    }
}