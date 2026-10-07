using Microsoft.AspNetCore.Mvc;
using Scootly.Application.Abstractions;

namespace Scootly.Mvc.ViewComponents;

public sealed class OpenTasksBadgeViewComponent : ViewComponent
{
    private readonly IFieldTaskReadService _fieldTaskReadService;

    public OpenTasksBadgeViewComponent(IFieldTaskReadService fieldTaskReadService)
    {
        _fieldTaskReadService = fieldTaskReadService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var count = await _fieldTaskReadService.GetOpenTaskCountAsync();
        return View(count);
    }
}
