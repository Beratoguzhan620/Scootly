namespace Scootly.Application.Abstractions;

public interface ICurrentUser
{
    Guid UserId { get; }
    string Role { get; }
}