using WasabiBot.Core.Models;

namespace WasabiBot.Core.Extensions;

public static class ResultExtensions
{
    public static async Task<Result<T>> Try<T>(this Task<T> task)
    {
        try
        {
            return await task;
        }
        catch (Exception e)
        {
            return Result<T>.Fail(e);
        }
    }
    
    public static async Task<Result> Try(this Task task)
    {
        try
        {
            await task;
            return Result.Ok();
        }
        catch (Exception e)
        {
            return e;
        }
    }
    
    public static async Task<Result> TryDropValue<T>(this Task<T> task)
    {
        try
        {
            await task;
            return Result.Ok();
        }
        catch (Exception e)
        {
            return e;
        }
    }

    public static Result DropValue<T>(this Result<T> result)
    {
        return result.IsOk ? Result.Ok() : Result.Fail(result.Error);
    }
}