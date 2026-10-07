using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Scootly.Infrastructure.Authorization;
using Scootly.Api.Contracts.Requests;
using Scootly.Api.Contracts.Responses;
using Scootly.Api.ErrorHandling;
using Scootly.Api.Extensions;
using Scootly.Api.Validators;
using Scootly.Application.Abstractions;
using Scootly.Application.Fleet.Commands;
using Scootly.Infrastructure.Geo;

namespace Scootly.Api.Controllers;

/// <summary>Hizmet bölgeleri: canlı filo bildirimleri bu bölgelere göre gruplanır.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/service-areas")]
public sealed class ServiceAreasController : ControllerBase
{
    private readonly IApplicationDbContext _dbContext;

    public ServiceAreasController(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Anonymous)]
    [ProducesResponseType<IReadOnlyList<ServiceAreaResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var areas = await _dbContext.ServiceAreas.AsNoTracking().OrderBy(a => a.Name).ToListAsync(cancellationToken);

        return Ok(areas.Select(a => new ServiceAreaResponse(
            a.Name,
            a.Boundary.Select(p => new BoundaryPointResponse(p.Latitude, p.Longitude)).ToList())));
    }

    [HttpPost]
    [Authorize(Policy = PolicyNames.FleetManagerOnly)]
    [EnableRateLimiting(RateLimitPolicies.User)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateServiceAreaRequest request,
        [FromServices] CreateServiceAreaRequestValidator validator,
        [FromServices] CreateServiceAreaCommandHandler handler,
        [FromServices] IMemoryCache memoryCache,
        CancellationToken cancellationToken)
    {
        var validation = validator.Validate(request);

        if (!validation.IsValid)
            return this.BadRequestProblem(validation.Error!);

        var result = await handler.Handle(
            new CreateServiceAreaCommand(request.Name, request.Boundary.Select(p => new BoundaryPoint(p.Latitude, p.Longitude)).ToList()),
            cancellationToken);

        if (!result.IsSuccess)
            return this.ToProblem(result);

        ServiceAreaRegionResolver.Invalidate(memoryCache);

        return StatusCode(StatusCodes.Status201Created);
    }
}
