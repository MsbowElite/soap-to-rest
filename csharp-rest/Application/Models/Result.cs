namespace CsharpRest.Application.Models;

public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsNotFound { get; }
    public string? ErrorMessage { get; }
    public T? Value { get; }

    private Result(bool isSuccess, bool isNotFound, T? value, string? errorMessage)
    {
        IsSuccess = isSuccess;
        IsNotFound = isNotFound;
        Value = value;
        ErrorMessage = errorMessage;
    }

    public static Result<T> Success(T value) => new(true, false, value, null);
    public static Result<T> NotFound(string? message = null) => new(false, true, default, message);
    public static Result<T> Failure(string? message) => new(false, false, default, message);
}
