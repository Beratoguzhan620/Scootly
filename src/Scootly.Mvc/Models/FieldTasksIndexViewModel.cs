using Scootly.Application.Abstractions;

namespace Scootly.Mvc.Models;

public sealed record FieldTasksIndexViewModel(
    IReadOnlyList<FieldTaskSummary> Open,
    IReadOnlyList<CompletedFieldTaskSummary> Completed,
    bool PhotoUploadEnabled);
