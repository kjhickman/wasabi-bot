namespace WasabiBot.Api.Features.RemindMe.Services;

internal sealed class ReminderTimeCalculator
{
    private static readonly TimeZoneInfo DefaultTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
    private readonly TimeProvider _timeProvider;

    public ReminderTimeCalculator(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Computes a future UTC timestamp by applying relative offsets to the current time.
    /// Returns null when the combination is invalid (negative values or all zero).
    /// </summary>
    public DateTimeOffset? ComputeRelativeUtc(int months = 0, int weeks = 0, int days = 0, int hours = 0, int minutes = 0, int seconds = 0)
    {
        if (ContainsNegative(months, weeks, days, hours, minutes, seconds))
        {
            return null;
        }

        if (AreAllZero(months, weeks, days, hours, minutes, seconds))
        {
            return null;
        }

        try
        {
            var totalDays = checked(weeks * 7 + days);
            return _timeProvider.GetUtcNow()
                .AddMonths(months)
                .AddDays(totalDays)
                .AddHours(hours)
                .AddMinutes(minutes)
                .AddSeconds(seconds);
        }
        catch
        {
            return null;
        }
    }

    private static bool AreAllZero(params int[] values)
    {
        return values.All(value => value == 0);
    }

    private static bool ContainsNegative(params int[] values)
    {
        return values.Any(value => value < 0);
    }

    /// <summary>
    /// Computes a future (or adjusted) UTC timestamp from calendar components.
    /// Returns null for invalid inputs. If year is 0, current year is assumed; if resulting time is past, a 1-year roll forward is applied.
    /// </summary>
    public DateTimeOffset? ComputeAbsoluteUtc(int month, int day, int year = 0, int hour = -1, int minute = -1)
    {
        if (month is < 1 or > 12 || day < 1 || hour < -1 || hour > 23 || minute < -1 || minute > 59)
            return null;

        var nowUtc = _timeProvider.GetUtcNow();
        var nowLocal = nowUtc.ToOffset(DefaultTimeZone.BaseUtcOffset);

        if (year == 0)
        {
            year = nowLocal.Year;
        }
        else if (year < nowLocal.Year || year > 2200)
        {
            return null;
        }

        // Resolve desired hours/minutes
        (hour, minute) = (hour, minute) switch
        {
            (-1, -1) => (nowLocal.Hour, nowLocal.Minute),
            (-1, _) => (0, minute),
            (_, -1) => (hour, 0),
            _ => (hour, minute)
        };

        try
        {
            var unspecified = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
            var offset = DefaultTimeZone.GetUtcOffset(unspecified);
            var dto = new DateTimeOffset(unspecified, offset);
            if (dto < nowLocal)
            {
                // Roll forward a year when the requested date already passed this calendar year.
                dto = dto.AddYears(1);
            }
            return dto.ToUniversalTime();
        }
        catch
        {
            return null;
        }
    }
}
