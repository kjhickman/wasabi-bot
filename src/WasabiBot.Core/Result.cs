using System.Diagnostics.CodeAnalysis;

namespace WasabiBot.Core;

public class Result<T>
{
    // Success constructor
    private Result(T value)
    {
        IsOk = true;
        Value = value;
        Error = null;
    }

    // Failure constructors
    private Result(Exception error)
    {
        IsOk = false;
        Value = default;
        Error = error;
    }
    
    private Result(string error)
    {
        IsOk = false;
        Error = new Exception(error);
    }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsOk { get; }
    public T? Value { get; }
    public Exception? Error { get; }

    // Helper methods for constructing the `Result<T>`
    public static Result<T> Ok(T value) => new(value);
    public static Result<T> Fail(Exception error) => new(error);
    public static Result<T> Fail(string error) => new(error);
    
    // Allow converting a T directly into Result<T>
    public static implicit operator Result<T>(T value) => Ok(value);
    public static implicit operator Result<T>(Exception ex) => new(ex);
}

public class Result
{
    // Success constructor
    private Result()
    {
        IsOk = true;
        Error = null;
    }
    
    // Failure constructors
    private Result(Exception error)
    {
        IsOk = false;
        Error = error;
    }
    
    private Result(string error)
    {
        IsOk = false;
        Error = new Exception(error);
    }
    
    public bool IsOk { get; }
    public bool IsError => !IsOk;
    public Exception? Error { get; }
    
    // Helper methods for constructing the `Result`
    public static Result Ok() => new();
    public static Result Fail(string error) => new(error);
    public static Result Fail(Exception error) => new(error);
    
    public static implicit operator Result(Exception ex) => new(ex);
}