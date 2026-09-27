namespace Scootly.Application.Abstractions.Exceptions;

/// <summary>Veritabanındaki bir benzersizlik kısıtı (bkz. <see cref="Common.ConstraintNames"/>) ihlal edildi.</summary>
public sealed class UniqueConstraintViolationException : Exception
{
    public string? ConstraintName { get; }

    public UniqueConstraintViolationException(string? constraintName, Exception? innerException = null)
        : base($"Benzersizlik kısıtı ihlal edildi: {constraintName ?? "bilinmiyor"}", innerException)
    {
        ConstraintName = constraintName;
    }
}
