namespace Scootly.Application.Abstractions;

public sealed record FieldTaskSummary(
    Guid Id,
    Guid VehicleId,
    string Type,
    string Status,
    Guid? AssignedTo,
    DateTime CreatedAt);

public interface IFieldTaskReadService
{
    Task<int> GetOpenTaskCountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FieldTaskSummary>> GetOpenTasksAsync(CancellationToken cancellationToken = default);
}