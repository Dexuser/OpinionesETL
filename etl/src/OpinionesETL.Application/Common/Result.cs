namespace OpinionesETL.Application.Common;

public class Result
{
    protected Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error? Error { get; }

    public static Result Success() => new(true, null);
    public static Result<T> Success<T>(T data) => new(data, true, null);

    public static implicit operator Result(Error error) => new(false, error);
}

public sealed class Result<T> : Result
{
    internal Result(T? data, bool isSuccess, Error? error) : base(isSuccess, error)
        => Data = data;

    public T? Data { get; }

    public static implicit operator Result<T>(Error error) => new(default, false, error);
}
