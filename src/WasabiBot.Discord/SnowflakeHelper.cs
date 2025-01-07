namespace WasabiBot.Core.Discord;

public static class SnowflakeHelper
{
    public static DateTimeOffset ConvertToDateTimeOffset(long snowflakeId)
    {
        const long epoch = 1420070400000;
        
        // Extract timestamp (shift right 22 bits to discard machine ID and sequence number)
        var timestamp = (snowflakeId >> 22) + epoch;

        // Convert timestamp from milliseconds to DateTime
        return DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
    }
}