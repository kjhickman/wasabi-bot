namespace WasabiBot.IntegrationTests.Infrastructure;

internal static class PostgresTimestamp
{
    public static DateTimeOffset Normalize(DateTimeOffset value)
    {
        var utcTicks = value.UtcDateTime.Ticks;
        var truncatedTicks = utcTicks - (utcTicks % 10);
        return new DateTimeOffset(new DateTime(truncatedTicks, DateTimeKind.Utc));
    }

    public static DateTimeOffset? Normalize(DateTimeOffset? value) =>
        value is null ? null : Normalize(value.Value);
}
