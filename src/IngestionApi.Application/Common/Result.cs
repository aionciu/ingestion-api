namespace IngestionApi.Application.Common;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsConflict { get; }
    public string? Error { get; }

    protected Result(bool isSuccess, bool isConflict, string? error)
    {
        IsSuccess = isSuccess;
        IsConflict = isConflict;
        Error = error;
    }

    public static Result Success() => new(true, false, null);
    public static Result Failure(string error) => new(false, false, error);
    public static Result ValidationFailure(string error = "Validation failed") => new(false, false, error);
    public static Result Conflict() => new(false, true, "A conflict occurred with existing data.");
    public static Result<T> Success<T>(T value) => new(value, true, false, null);
    public static Result<T> Failure<T>(string error) => new(default, false, false, error);
    public static Result<T> Conflict<T>() => new(default, false, true, "A conflict occurred with existing data.");
}

public sealed class Result<T> : Result
{
    public T? Value { get; }

    internal Result(T? value, bool isSuccess, bool isConflict, string? error) : base(isSuccess, isConflict, error)
    {
        Value = value;
    }
}
