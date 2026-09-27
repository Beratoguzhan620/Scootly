namespace Scootly.Application.Abstractions;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Doğrulanmış kullanıcının kimliği. Geçerli bir kullanıcı yoksa <see cref="UnauthorizedAccessException"/> fırlatır.</summary>
    Guid UserId { get; }
}
