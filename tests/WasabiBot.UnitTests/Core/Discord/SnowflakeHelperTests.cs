using WasabiBot.Core.Discord;

namespace WasabiBot.UnitTests.Core.Discord;

public class SnowflakeHelperTests
{
    [Test]
    public async Task ConvertToDateTimeOffset_ValidSnowflake_ReturnsDateTimeOffset()
    {
        const long snowflakeId = 1293779028234211358;
        var expectedDateTimeOffset = DateTimeOffset.Parse("2024-10-10 03:35:57.583Z");
        
        var result = SnowflakeHelper.ConvertToDateTimeOffset(snowflakeId);
        
        await Assert.That(result).IsEqualTo(expectedDateTimeOffset);
    }
}