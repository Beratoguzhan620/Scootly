using Scootly.Domain.Geo;

namespace Scootly.Application.Abstractions;

public interface IServiceAreaRepository
{
    Task AddAsync(ServiceArea serviceArea, CancellationToken cancellationToken = default);
}
