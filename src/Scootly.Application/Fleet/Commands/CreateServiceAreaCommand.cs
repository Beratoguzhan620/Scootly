using Scootly.Application.Abstractions;
using Scootly.Application.Abstractions.Exceptions;
using Scootly.Application.Common;
using Scootly.Domain.Common;
using Scootly.Domain.Geo;

namespace Scootly.Application.Fleet.Commands;

public sealed record BoundaryPoint(double Latitude, double Longitude);

public sealed record CreateServiceAreaCommand(string Name, IReadOnlyList<BoundaryPoint> Boundary);

public sealed class CreateServiceAreaCommandHandler
{
    private readonly IServiceAreaRepository _serviceAreaRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateServiceAreaCommandHandler(IServiceAreaRepository serviceAreaRepository, IUnitOfWork unitOfWork)
    {
        _serviceAreaRepository = serviceAreaRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(CreateServiceAreaCommand command, CancellationToken cancellationToken = default)
    {
        ServiceArea serviceArea;

        try
        {
            var boundary = command.Boundary.Select(p => new GeoPoint(p.Latitude, p.Longitude)).ToList();
            serviceArea = new ServiceArea(command.Name, boundary);
        }
        catch (DomainException ex)
        {
            return Result.Validation(ex.Message);
        }

        try
        {
            await _serviceAreaRepository.AddAsync(serviceArea, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException ex)
        {
            _unitOfWork.DiscardChanges();
            return Result.Failure(ConstraintNames.ToUserMessage(ex.ConstraintName));
        }

        return Result.Success();
    }
}
