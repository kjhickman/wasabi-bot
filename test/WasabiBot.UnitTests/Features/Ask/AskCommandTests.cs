using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenTelemetry.Trace;
using WasabiBot.Api.Features.Ask;
using WasabiBot.UnitTests.Infrastructure.Discord;

namespace WasabiBot.UnitTests.Features.Ask;

public class AskCommandTests
{
    private static AskCommand CreateCommand(IAskAnswerService askAnswerService)
    {
        var tracer = TracerProvider.Default.GetTracer("ask-tests");
        return new AskCommand(askAnswerService, tracer, NullLogger<AskCommand>.Instance);
    }

    [Test]
    public async Task ExecuteAsync_WhenResponseReceived_UsesAskServiceAndSendsReply()
    {
        var askAnswerService = Substitute.For<IAskAnswerService>();
        askAnswerService
            .AnswerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AskAnswer("Short answer")));

        var command = CreateCommand(askAnswerService);
        var context = new FakeCommandContext();

        const string question = "How many moons does Mars have?";
        await command.ExecuteAsync(context, question);

        await askAnswerService.Received(1).AnswerAsync(question, Arg.Any<CancellationToken>());

        await Assert.That(context.Messages.Count).IsEqualTo(1);
        var (message, ephemeral) = context.Messages.Single();
        await Assert.That(ephemeral).IsFalse();
        await Assert.That(message).IsEqualTo($"**Question:** {question}\n\nShort answer");
    }

    [Test]
    public async Task ExecuteAsync_WhenResponseIsLong_TruncatesReplyToDiscordLimit()
    {
        var askAnswerService = Substitute.For<IAskAnswerService>();
        askAnswerService
            .AnswerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AskAnswer(new string('a', 3_000))));

        var command = CreateCommand(askAnswerService);
        var context = new FakeCommandContext();

        await command.ExecuteAsync(context, "Why is this long?");

        var (message, ephemeral) = context.Messages.Single();
        await Assert.That(ephemeral).IsFalse();
        await Assert.That(message.Length).IsLessThanOrEqualTo(2_000);
        await Assert.That(message).Contains("Answer truncated to fit Discord's 2000 character limit.");
    }

    [Test]
    public async Task ExecuteAsync_WhenAskServiceThrows_SendsFriendlyError()
    {
        var askAnswerService = Substitute.For<IAskAnswerService>();
        askAnswerService
            .AnswerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AskAnswer>(new InvalidOperationException("outage")));

        var command = CreateCommand(askAnswerService);
        var context = new FakeCommandContext();

        await command.ExecuteAsync(context, "What time is it?");

        await Assert.That(context.EphemeralMessages.Count).IsEqualTo(1);
        await Assert.That(context.EphemeralMessages.Single()).IsEqualTo("I couldn't answer that right now. Please try again later.");
    }
}
