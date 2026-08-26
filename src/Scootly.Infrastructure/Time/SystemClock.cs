using Scootly.Application.Abstractions;

namespace Scootly.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}