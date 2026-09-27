using Scootly.Application.Abstractions;
using Scootly.Domain.Geo;

namespace Scootly.Infrastructure.Persistence.Repositories;

public sealed class ServiceAreaRepository : IServiceAreaRepository
{
    private readonly ScootlyDbContext _dbContext;

    public ServiceAreaRepository(ScootlyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(ServiceArea serviceArea, CancellationToken cancellationToken = default)
        => await _dbContext.ServiceAreas.AddAsync(serviceArea, cancellationToken);
}
