namespace Scootly.Domain.Common;

public enum ErrorType
{
    None,
    Validation,
    NotFound,
    Conflict,
    Forbidden
}

public class Result
{
    public bool IsSuccess { get; }
    public string Error { get; }
    public ErrorType ErrorType { get; }

    protected Result(bool isSuccess, string error, ErrorType errorType)
    {
        IsSuccess = isSuccess;
        Error = error;
        ErrorType = errorType;
    }

    public static Result Success() => new(true, string.Empty, ErrorType.None);

    /// <summary>İş kuralı çakışması (varsayılan hata türü).</summary>
    public static Result Failure(string error) => new(false, error, ErrorType.Conflict);

    public static Result Failure(string error, ErrorType errorType) => new(false, error, errorType);

    public static Result NotFound(string error) => new(false, error, ErrorType.NotFound);

    public static Result Validation(string error) => new(false, error, ErrorType.Validation);

    public static Result Forbidden(string error) => new(false, error, ErrorType.Forbidden);
}

public class Result<T> : Result
{
    public T? Value { get; }

    protected Result(T? value, bool isSuccess, string error, ErrorType errorType)
        : base(isSuccess, error, errorType)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(value, true, string.Empty, ErrorType.None);

    public static new Result<T> Failure(string error) => new(default, false, error, ErrorType.Conflict);

    public static new Result<T> Failure(string error, ErrorType errorType) => new(default, false, error, errorType);

    public static new Result<T> NotFound(string error) => new(default, false, error, ErrorType.NotFound);

    public static new Result<T> Validation(string error) => new(default, false, error, ErrorType.Validation);

    public static new Result<T> Forbidden(string error) => new(default, false, error, ErrorType.Forbidden);
}
