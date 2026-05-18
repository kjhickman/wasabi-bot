using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenTelemetry.Trace;
using WasabiBot.Api.Features.MagicConch;
using WasabiBot.UnitTests.Builders;
using WasabiBot.UnitTests.Infrastructure.Discord;

namespace WasabiBot.UnitTests.Features.MagicConch;

public class MagicConchCommandTests
{
    private static MagicConchCommand CreateCommand(IChatClient chatClient, IMagicConchTool? tool = null)
    {
        var tracer = TracerProvider.Default.GetTracer("magicconch-tests");
        tool ??= Substitute.For<IMagicConchTool>();
        return new MagicConchCommand(chatClient, tracer, NullLogger<MagicConchCommand>.Instance, tool);
    }

    [Test]
    public async Task ExecuteAsync_WhenResponseReceived_SendsFormattedAnswer()
    {
        var chatResponse = ChatResponseBuilder.Create()
            .WithAssistantText("Yes")
            .Build();

        var chatClient = Substitute.For<IChatClient>();
        chatClient
            .GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(chatResponse));

        var command = CreateCommand(chatClient);
        var context = new FakeCommandContext();

        const string question = "Will it rain tomorrow?";
        await command.ExecuteAsync(context, question);

        await chatClient.Received(1).GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());

        await Assert.That(context.Messages.Count).IsEqualTo(1);
        var (message, ephemeral) = context.Messages.Single();
        await Assert.That(ephemeral).IsFalse();
        await Assert.That(message.Contains(question)).IsTrue();
        await Assert.That(message.Contains("Magic Conch")).IsTrue();
        await Assert.That(message.Contains("Yes")).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_WhenResponseRequestsConchTool_InvokesToolAndSendsFormattedAnswer()
    {
        var chatResponse = ChatResponseBuilder.Create()
            .WithAssistantText("UseMagicConch")
            .Build();

        var chatClient = Substitute.For<IChatClient>();
        chatClient
            .GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(chatResponse));

        var tool = Substitute.For<IMagicConchTool>();
        tool.GetMagicConchResponse(Arg.Any<string>()).Returns("Maybe");

        var command = CreateCommand(chatClient, tool);
        var context = new FakeCommandContext();

        const string question = "Will I eat pizza tonight?";
        await command.ExecuteAsync(context, question);

        tool.Received(1).GetMagicConchResponse(question);

        await Assert.That(context.Messages.Count).IsEqualTo(1);
        var (message, ephemeral) = context.Messages.Single();
        await Assert.That(ephemeral).IsFalse();
        await Assert.That(message.Contains(question)).IsTrue();
        await Assert.That(message.Contains("Maybe")).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_WhenChatClientThrows_SendsFriendlyError()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient
            .GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ChatResponse>(new InvalidOperationException("outage")));

        var tool = Substitute.For<IMagicConchTool>();
        tool.GetMagicConchResponse(Arg.Any<string>()).Returns("Tool says fallback");

        var command = CreateCommand(chatClient, tool);
        var context = new FakeCommandContext();

        await command.ExecuteAsync(context, "Is anyone there?");

        tool.Received(1).GetMagicConchResponse(Arg.Any<string>());

        await Assert.That(context.Messages.Count).IsEqualTo(1);
        var (message, ephemeral) = context.Messages.Single();
        await Assert.That(ephemeral).IsFalse();
        await Assert.That(message.Contains("Tool says fallback")).IsTrue();
    }
}
