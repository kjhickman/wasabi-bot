using Microsoft.Extensions.Logging.Abstractions;
using NetCord;
using NetCord.JsonModels;
using NetCord.Rest;
using WasabiBot.Api.Features.Mock;

namespace WasabiBot.UnitTests.Features.Mock;

public class MockCommandTests
{
    private static MockCommand CreateCommand()
    {
        return new MockCommand(NullLogger<MockCommand>.Instance);
    }

    private static readonly BotToken TestBotToken = new("MTAwMDAwMDAwMDAwMDAwMDAw.NOT_A_REAL_TOKEN.TEST_ONLY");

    private static RestMessage CreateMessage(string content)
    {
        return new RestMessage(
            new JsonMessage
            {
                Id = 77,
                ChannelId = 1001,
                Content = content,
                Author = new JsonUser
                {
                    Id = 42,
                    Username = "TestUser",
                    Discriminator = 0
                },
                Attachments = [],
                Embeds = [],
                MentionedUsers = [],
                MentionedRoleIds = [],
                Components = []
            },
            new RestClient(TestBotToken));
    }

    [Test]
    public async Task Mockify_AlternatesLetterCasing()
    {
        var mocked = MockCommand.Mockify("mock this message");

        await Assert.That(mocked).IsEqualTo("MoCk ThIs MeSsAgE");
    }

    [Test]
    public async Task Mockify_PreservesNonLetterCharacters()
    {
        var mocked = MockCommand.Mockify("hi!!! 123 okay?");

        await Assert.That(mocked).IsEqualTo("Hi!!! 123 OkAy?");
    }

    [Test]
    public async Task ExecuteAsync_WithTextContent_ReturnsMockedPublicMessage()
    {
        var command = CreateCommand();
        var message = CreateMessage("please mock me");

        var response = await command.ExecuteAsync(message);

        await Assert.That(response.Content).IsEqualTo("PlEaSe MoCk Me");
        await Assert.That(response.Flags).IsNull();
    }

    [Test]
    public async Task ExecuteAsync_WithoutTextContent_ReturnsEphemeralError()
    {
        var command = CreateCommand();
        var message = CreateMessage("   ");

        var response = await command.ExecuteAsync(message);

        await Assert.That(response.Content).IsEqualTo("That message has no text to mock.");
        await Assert.That(response.Flags).IsEqualTo(MessageFlags.Ephemeral);
    }
}
