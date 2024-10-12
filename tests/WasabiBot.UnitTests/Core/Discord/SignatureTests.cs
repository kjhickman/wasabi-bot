using WasabiBot.Core.Discord;

namespace WasabiBot.UnitTests.Core.Discord;

public class SignatureTests
{
    [Test]
    public async Task Verify_ValidSignature_ReturnsTrue()
    {
        // Arrange
        const string publicKey = "6a897adddeb5bc997711be27b245235a58752532beceea87eb4d21abadcfb1c2";
        const string signature = "6fd0043b361dafbe5595d41b6161a7a0712587faa6b4b86a9fdfc466ac00db3e9e7da905095747cfd579ab283440bdccaa25e1de6430c6fa186b5522adc21002";
        const string timestamp = "1727930315";
        const string requestBody = "{\"app_permissions\":\"562949953601536\",\"application_id\":\"1039617611291439126\",\"authorizing_integration_owners\":{},\"entitlements\":[],\"id\":\"1291258073104777276\",\"token\":\"aW50ZXJhY3Rpb246MTI5MTI1ODA3MzEwNDc3NzI3Njp4UWJFZXZGdEFiem5rRENCcENaalFma0doN3g4Zzk1Y0N2ZEYxbGUxbGU4NnNLQmJjc0Q1dTZLeTdqVU1GQkhpZk5Bb2U2enNqU3hpMjVwQ3l6NlZsR2Y0Z2JoSlk1b1NnWjRzczI4aEsxR3FiUU11OGpaa1dFMUlSdTd5N25JNA\",\"type\":1,\"user\":{\"avatar\":\"c6a249645d46209f337279cd2ca998c7\",\"avatar_decoration_data\":null,\"bot\":true,\"clan\":null,\"discriminator\":\"0000\",\"global_name\":\"Discord\",\"id\":\"643945264868098049\",\"public_flags\":1,\"system\":true,\"username\":\"discord\"},\"version\":1}";

        // Act
        var result = Signature.Verify(publicKey, signature, timestamp, requestBody);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Verify_InvalidSignature_ReturnsFalse()
    {
        // Arrange
        const string publicKey = "6a897adddeb5bc997711be27b245235a58752532beceea87eb4d21abadcfb1c2";
        const string signature = "68f6812370c2e4b01a51c6053a44971936b97700195770c046e1fc7e9c90187e5f18b1f03192716d963ca109506e235bf82c6f677ecb4ba9456d9677e5bee905";
        const string timestamp = "1717788327";
        const string requestBody = "{\"app_permissions\":\"180224\",\"application_id\":\"1041227469367287838\",\"authorizing_integration_owners\":{},\"entitlements\":[],\"id\":\"1248719492016640111\",\"token\":\"aW50ZXJhY3Rpb246MTI0ODcxOTQ5MjAxNjY0MDExMTprVzBzakcyZUZkOFhDRWg5MUxDd0kzOTJuMG1HNWN0blp2a25Xc0l4VU52Y2ozZVlUcHVHbzRUTTh3cGk3N3d1cmc0Z1VkRVpmY0xuWnlKWHBuaXVHUk1keGNrYmdkamM5Q1ltcUZPMkU3R2dyZ1RkQm9JeW5FZmlwUUNaUlpIZg\",\"type\":1,\"user\":{\"avatar\":\"c6a249645d46209f337279cd2ca998c7\",\"avatar_decoration_data\":null,\"bot\":true,\"clan\":null,\"discriminator\":\"0000\",\"global_name\":\"Discord\",\"id\":\"643945264868098049\",\"public_flags\":1,\"system\":true,\"username\":\"discord\"},\"version\":1}";

        // Act
        var result = Signature.Verify(publicKey, signature, timestamp, requestBody);

        // Assert
        await Assert.That(result).IsFalse();
    }
}