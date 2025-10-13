namespace WasabiBot.IntegrationTests.Assertions;

/// <summary>
/// Provides helper assertions for comparing DateTimeOffset values where the data store truncates
/// sub-millisecond precision (e.g. PostgreSQL microsecond precision vs .NET ticks).
/// </summary>
internal static class TimeAssert
{
    /// <summary>
    /// Asserts equality after trimming both timestamps to PostgreSQL's microsecond precision.
    /// This avoids false negatives due to sub-microsecond tick differences (100ns) that PostgreSQL does not persist.
    /// </summary>
    /// <remarks>
    /// PostgreSQL stores timestamps with microsecond precision (6 fractional digits). .NET DateTimeOffset has 100ns ticks.
    /// This method removes the sub-microsecond remainder (ticks % 10) before comparing.
    /// </remarks>
    public static void WithinPostgresPrecision(DateTimeOffset expected, DateTimeOffset actual, string? message = null)
    {
        var e = Trim(expected);
        var a = Trim(actual);
        if (e != a)
        {
            var details = $"Expected (pg precision) {e:o} Actual {a:o}. Raw Expected {expected:o} Actual {actual:o}.";
            if (!string.IsNullOrWhiteSpace(message))
            {
                details += " " + message;
            }
            throw new Xunit.Sdk.XunitException(details);
        }

        return;

        static DateTimeOffset Trim(DateTimeOffset value)
        {
            // 1 microsecond = 10 ticks (each tick = 100ns)
            var trimmedTicks = value.Ticks - (value.Ticks % 10);
            return new DateTimeOffset(trimmedTicks, value.Offset);
        }
    }
}
