namespace Scootly.Api.Contracts.Responses;

public sealed record ApiErrorResponse(
    string Title,
    string Detail,
    int StatusCode);