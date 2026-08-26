namespace Scootly.Application.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}