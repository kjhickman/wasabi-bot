namespace WasabiBot.Api.Services;

internal sealed class NaturalLanguageTimeResolver : INaturalLanguageTimeResolver
{
    public DateTimeOffset? ComputeRelativeUtc(int months = 0, int weeks = 0, int days = 0, int hours = 0, int minutes = 0, int seconds = 0)
    {
        if (months < 0 || weeks < 0 || days < 0 || hours < 0 || minutes < 0 || seconds < 0)
            return null;

        if (months == 0 && weeks == 0 && days == 0 && hours == 0 && minutes == 0 && seconds == 0)
            return null;

        try
        {
            return DateTimeOffset.UtcNow
                .AddMonths(months)
                .AddDays(weeks * 7 + days)
                .AddHours(hours)
                .AddMinutes(minutes)
                .AddSeconds(seconds);
        }
        catch
        {
            return null;
        }
    }

    public DateTimeOffset? ComputeAbsoluteUtc(int month, int day, int year = 0, int hour = -1, int minute = -1, string? timeZoneId = null)
    {
        if (month is < 1 or > 12 || day < 1 || hour < -1 || hour > 23 || minute < -1 || minute > 59)
            return null;

        var timeZone = ResolveTimeZone(timeZoneId);
        var nowLocal = DateTimeOffset.UtcNow.ToOffset(timeZone.BaseUtcOffset);

        if (year == 0)
            year = nowLocal.Year;
        if (year < nowLocal.Year)
            return null;

        if (hour == -1 && minute == -1)
        {
            hour = nowLocal.Hour;
            minute = nowLocal.Minute;
        }
        if (hour == -1) hour = 0;
        if (minute == -1) minute = 0;

        try
        {
            var unspecified = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
            var offset = timeZone.GetUtcOffset(unspecified);
            var dto = new DateTimeOffset(unspecified, offset);
            if (dto < nowLocal)
                dto = dto.AddYears(1);
            return dto.ToUniversalTime();
        }
        catch
        {
            return null;
        }
    }

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        }
    }
}

