using System;
using System.Diagnostics.CodeAnalysis;

namespace PDV.Domain.Common.Models;

public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new ArgumentException("El resultado exitoso no puede contener un error.", nameof(error));
        }

        if (!isSuccess && error == Error.None)
        {
            throw new ArgumentException("El resultado fallido debe contener un error.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

public class Result<TValue> : Result
{
    private readonly TValue? _value;

    protected internal Result(TValue? value, bool isSuccess, Error error) : base(isSuccess, error)
    {
        _value = value;
    }

    [NotNull]
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("No se puede acceder al valor de un resultado de fallo.");

    public static implicit operator Result<TValue>(TValue value) => Success(value);
}
