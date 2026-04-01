using Microsoft.Extensions.Logging.Abstractions;
using WasabiBot.Api.Features.Flip;
using WasabiBot.UnitTests.Infrastructure.Discord;

namespace WasabiBot.UnitTests.Features.Flip;

public class FlipCommandTests
{
    private sealed class DeterministicRandom(int fixedValue) : Random
    {
        private readonly int _fixedValue = fixedValue;

        public override int Next(int maxValue) => _fixedValue % maxValue;
    }

    private static FlipCommand CreateCommand()
    {
        return new FlipCommand(NullLogger<FlipCommand>.Instance);
    }

    [Test]
    public async Task FlipCoin_WithZeroRandom_ReturnsHeads()
    {
        var result = FlipCommand.FlipCoin(new DeterministicRandom(0));

        await Assert.That(result).IsEqualTo("Heads");
    }

    [Test]
    public async Task FlipCoin_WithOneRandom_ReturnsTails()
    {
        var result = FlipCommand.FlipCoin(new DeterministicRandom(1));

        await Assert.That(result).IsEqualTo("Tails");
    }

    [Test]
    public async Task ExecuteAsync_SendsPlayfulPublicMessage()
    {
        var command = CreateCommand();
        var context = new FakeCommandContext(userId: 42, channelId: 1001, userDisplayName: "TestUser");

        await command.ExecuteAsync(context);

        await Assert.That(context.Messages.Count).IsEqualTo(1);

        var (message, ephemeral) = context.Messages.Single();
        await Assert.That(ephemeral).IsFalse();
        await Assert.That(message).Contains("The coin lands on...");
        await Assert.That(message.Contains("Heads!") || message.Contains("Tails!")).IsTrue();
    }
}
