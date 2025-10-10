namespace WasabiBot.Api.Features.RemindMe.Services;

internal sealed class NaturalLanguageTimeResolver : INaturalLanguageTimeResolver
{
    private static readonly TimeZoneInfo DefaultTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
    private readonly TimeProvider _timeProvider;

    public NaturalLanguageTimeResolver(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

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
