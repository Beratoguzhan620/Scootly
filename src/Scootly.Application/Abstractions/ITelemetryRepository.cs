using Scootly.Domain.Telemetry;

namespace Scootly.Application.Abstractions;

public interface ITelemetryRepository
{
    void AddRange(IEnumerable<TelemetryReading> readings);
}
