using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace WasabiBot.Api.Services;

/// <summary>
/// Provides AI function tool definitions that delegate to <see cref="INaturalLanguageTimeResolver"/>.
/// This isolates time computation logic from command modules and enables focused unit testing.
/// </summary>
internal sealed class NaturalLanguageTimeToolProvider
{
    private readonly INaturalLanguageTimeResolver _resolver;
    public ChatOptions Options { get; }

    public NaturalLanguageTimeToolProvider(INaturalLanguageTimeResolver resolver)
    {
        _resolver = resolver;
        var relative = AIFunctionFactory.Create(RelativeTime);
        var absolute = AIFunctionFactory.Create(AbsoluteTime);
        Options = new ChatOptions { Tools = [relative, absolute] };
    }

    [Description("Computes the target UTC time by adding relative offsets to the current UTC time. Returns null when invalid or zero-length.")]
    private DateTimeOffset? RelativeTime(
        [Description("Months from now (>=0)." )] int months = 0,
        [Description("Weeks from now (>=0)."  )] int weeks = 0,
        [Description("Days from now (>=0)."   )] int days = 0,
        [Description("Hours from now (>=0)."  )] int hours = 0,
        [Description("Minutes from now (>=0)." )] int minutes = 0,
        [Description("Seconds from now (>=0)." )] int seconds = 0)
    {
        return _resolver.ComputeRelativeUtc(months, weeks, days, hours, minutes, seconds);
    }

    [Description("Computes a target UTC time from calendar components; year optional, time optional, rolls forward if past. Returns null when invalid.")]
    private DateTimeOffset? AbsoluteTime(
        [Description("Calendar month 1-12.")] int month,
        [Description("Calendar day 1-31.")] int day,
        [Description("Calendar year (0 = current year)." )] int year = 0,
        [Description("Hour 0-23 (-1 = infer)." )] int hour = -1,
        [Description("Minute 0-59 (-1 = infer)." )] int minute = -1,
        [Description("Time zone ID (IANA or Windows)." )] string? timeZoneId = null)
    {
        return _resolver.ComputeAbsoluteUtc(month, day, year, hour, minute, timeZoneId);
    }
}
