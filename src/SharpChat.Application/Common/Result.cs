using System.Diagnostics.CodeAnalysis;

namespace SharpChat.Application.Common;

/// <summary>Outcome of a use case: either a value or an <see cref="Common.Error"/>. Expected failures never throw.</summary>
public sealed class Result<T>
{
    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }
    public Error? Error { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

#pragma warning disable CA1000 // Factory methods on a generic result type are the clearest API here
    public static Result<T> Success(T value) => new(value, null);

    public static Result<T> Failure(Error error) => new(default, error);
#pragma warning restore CA1000

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
