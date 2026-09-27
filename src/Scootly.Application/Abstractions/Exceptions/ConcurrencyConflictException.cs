namespace Scootly.Application.Abstractions.Exceptions;

/// <summary>Kaydedilmek istenen satır, okunduktan sonra başka bir işlem tarafından değiştirildi.</summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
