namespace YAFT.Application.Common;

public enum ErrorType { Validation, NotFound, Unauthorized, Conflict, Unavailable }

public sealed record Error(string Code, string Description, ErrorType Type);

public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T value) { _value = value; }
    private Result(Error error) { Error = error; }

    public bool IsSuccess => Error is null;
    public Error? Error { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Il risultato è un errore.");

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => new(value);
    public static implicit operator Result<T>(Error error) => new(error);
}

/// <summary>Risultato di un comando che non restituisce valori.</summary>
public sealed class Result
{
    private static readonly Result Ok = new(null);

    private Result(Error? error) { Error = error; }

    public bool IsSuccess => Error is null;
    public Error? Error { get; }

    public static Result Success() => Ok;
    public static Result Failure(Error error) => new(error);

    public static implicit operator Result(Error error) => new(error);
}
