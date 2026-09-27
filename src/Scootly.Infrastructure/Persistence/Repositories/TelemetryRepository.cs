using Scootly.Application.Abstractions;
using Scootly.Domain.Telemetry;

namespace Scootly.Infrastructure.Persistence.Repositories;

public sealed class TelemetryRepository : ITelemetryRepository
{
    private readonly ScootlyDbContext _dbContext;

    public TelemetryRepository(ScootlyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void AddRange(IEnumerable<TelemetryReading> readings) => _dbContext.TelemetryReadings.AddRange(readings);
}
