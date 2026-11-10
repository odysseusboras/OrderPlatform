namespace OrderPlatform.BuildingBlocks.Results;

/// <summary>An expected failure: a stable machine-readable code and a message for people.</summary>
public sealed record Error(string Code, string Message);

/// <summary>The outcome of an operation that can fail in an expected way. Unexpected failures stay exceptions.</summary>
public sealed class Result<T>
{
    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public T? Value { get; }

    public Error? Error { get; }

    public static Result<T> Success(T value) => new(value, null);

    public static Result<T> Failure(Error error) => new(default, error ?? throw new ArgumentNullException(nameof(error)));
}
