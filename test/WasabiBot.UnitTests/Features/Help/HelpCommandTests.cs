using Microsoft.Extensions.Logging.Abstractions;
using WasabiBot.Api.Features.Help;
using WasabiBot.UnitTests.Infrastructure.Discord;

namespace WasabiBot.UnitTests.Features.Help;

public class HelpCommandTests
{
    private static HelpCommand CreateCommand()
    {
        return new HelpCommand(NullLogger<HelpCommand>.Instance);
    }

    [Test]
    public async Task ExecuteAsync_RespondsWithHelpMessage()
    {
        var command = CreateCommand();
        var context = new FakeCommandContext(userId: 42, userDisplayName: "TestUser");

        await command.ExecuteAsync(context);

        var ephemerals = context.EphemeralMessages;
        await Assert.That(ephemerals.Count).IsEqualTo(1);
        await Assert.That(ephemerals.Single()).IsNotEmpty();
        await Assert.That(ephemerals.Single().Contains("`/play` - Play music from a direct track or playlist URL")).IsTrue();
        await Assert.That(ephemerals.Single().Contains("`/ask` - Ask any question")).IsTrue();
    }
}
