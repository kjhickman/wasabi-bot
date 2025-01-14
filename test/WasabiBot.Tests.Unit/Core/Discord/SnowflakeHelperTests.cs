using FluentAssertions;
using WasabiBot.Discord;

namespace WasabiBot.Tests.Unit.Core.Discord;

public class SnowflakeHelperTests
{
    [Fact]
    public void ConvertToDateTimeOffset_ValidSnowflake_ReturnsDateTimeOffset()
    {
        const long snowflakeId = 1293779028234211358;
        var expectedDateTimeOffset = DateTimeOffset.Parse("2024-10-10 03:35:57.583Z");
        
        var result = SnowflakeHelper.ConvertToDateTimeOffset(snowflakeId);

        result.Should().Be(expectedDateTimeOffset);
    }
}