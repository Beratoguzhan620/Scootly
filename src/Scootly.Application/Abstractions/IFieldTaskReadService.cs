namespace Scootly.Application.Abstractions;

public sealed record FieldTaskSummary(
    Guid Id,
    Guid VehicleId,
    string Type,
    string Status,
    Guid? AssignedTo,
    DateTime CreatedAt);

public sealed record CompletedFieldTaskSummary(
    Guid Id,
    Guid VehicleId,
    string Type,
    DateTime? CompletedAt,
    bool HasPhoto);

public interface IFieldTaskReadService
{
    Task<int> GetOpenTaskCountAsync(CancellationToken cancellationToken = default);

    /// <summary>En eski açık görevler (en fazla <paramref name="take"/> adet).</summary>
    Task<IReadOnlyList<FieldTaskSummary>> GetOpenTasksAsync(int take, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CompletedFieldTaskSummary>> GetRecentCompletedTasksAsync(int take, CancellationToken cancellationToken = default);

    Task<string?> GetPhotoObjectKeyAsync(Guid fieldTaskId, CancellationToken cancellationToken = default);
}
